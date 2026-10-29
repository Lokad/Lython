using System.Buffers;
using System.Collections.ObjectModel;
using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Utf8Regex;

/// <summary>Optional PythonRe adapter with borrowed UTF-8 subjects and explicit storage owners.</summary>
public sealed class Utf8RegexProvider : ILythonRegexProvider
{
    /// <summary>Gets a stateless provider instance; patterns and working owners remain caller-local.</summary>
    public static Utf8RegexProvider Instance { get; } = new();

    /// <inheritdoc />
    public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
        ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = memoryOwner.CreateScope();
        try
        {
            // Major retained compiler tables plus construction overlap. Count
            // possible slots before compilation; escaped parentheses may overpay.
            var slots = 1;
            for (var i = 0; i < pattern.Length; i++)
            {
                if ((i & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (pattern.Span[i] == (byte)'(') slots++;
            }
            Reserve(owner, checked(65536L + 2048L * Math.Max(0, slots - 2) + 256L * Math.Max(0, pattern.Length - 64)));
            var backendOptions = Options(options.Flags);
            backendOptions = options.Verbose ? backendOptions | PythonReCompileOptions.Verbose : backendOptions & ~PythonReCompileOptions.Verbose;
            var regex = Backend(() => Utf8PythonRegex.Compile(pattern.Span, backendOptions,
                Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
                Timeout.InfiniteTimeSpan, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            return new Pattern(regex, EffectiveFlags(regex.Pattern, options, cancellationToken), pattern.Length, owner);
        }
        catch { owner.Dispose(); throw; }
    }

    private static T Backend<T>(Func<T> work)
    {
        try { return work(); }
        catch (PythonRePatternException error)
        { throw new LythonRegexException(LythonRegexFailure.Pattern, error.Message, error.Position); }
        catch (ArgumentException error)
        { throw new LythonRegexException(LythonRegexFailure.Pattern, error.Message); }
    }

    private static ILythonMemoryReservation Reserve(ILythonMemoryBudget owner, long bytes)
    {
        if (!owner.TryReserve(bytes, out var storage))
            throw new LythonRegexException(LythonRegexFailure.MemoryAdmission,
                "Shared memory budget refused regex storage.", requestedBytes: bytes);
        return storage;
    }

    private static void Resize(ILythonMemoryReservation storage, long bytes)
    {
        bytes = Math.Max(bytes, storage.Bytes);
        if (!storage.TryResize(bytes))
            throw new LythonRegexException(LythonRegexFailure.MemoryAdmission,
                "Shared memory budget refused regex storage.", requestedBytes: bytes - storage.Bytes);
    }

    private static PythonReCompileOptions Options(int flags)
    {
        var value = PythonReCompileOptions.None;
        if ((flags & 2) != 0) value |= PythonReCompileOptions.IgnoreCase;
        if ((flags & 8) != 0) value |= PythonReCompileOptions.Multiline;
        if ((flags & 16) != 0) value |= PythonReCompileOptions.DotAll;
        if ((flags & 64) != 0) value |= PythonReCompileOptions.Verbose;
        if ((flags & 256) != 0) value |= PythonReCompileOptions.Ascii;
        return value;
    }

    private static int EffectiveFlags(string pattern, LythonRegexOptions options, CancellationToken token)
    {
        // Options reports constructor flags; read only the validated global prefix.
        var flags = options.Flags;
        var verbose = options.Verbose;
        var offset = 0;
        while (offset < pattern.Length)
        {
            token.ThrowIfCancellationRequested();
            if (verbose && pattern[offset] is ' ' or '\t' or '\r' or '\n' or '\v' or '\f') { offset++; continue; }
            if (verbose && pattern[offset] == '#')
            {
                while (offset < pattern.Length && pattern[offset] != '\n')
                {
                    token.ThrowIfCancellationRequested();
                    offset += pattern[offset] == '\\' && offset + 1 < pattern.Length ? 2 : 1;
                }
                continue;
            }
            if (pattern.AsSpan(offset).StartsWith("(?#"))
            {
                offset += 3;
                while (offset < pattern.Length && pattern[offset] != ')')
                {
                    token.ThrowIfCancellationRequested();
                    offset += pattern[offset] == '\\' && offset + 1 < pattern.Length ? 2 : 1;
                }
                offset++; continue;
            }
            if (!pattern.AsSpan(offset).StartsWith("(?")) break;
            var end = offset + 2;
            var nextFlags = flags;
            var nextVerbose = verbose;
            while (end < pattern.Length)
            {
                token.ThrowIfCancellationRequested();
                var flag = pattern[end] switch { 'i' => 2, 'm' => 8, 's' => 16, 'x' => 64, 'a' => 256, 'u' => 32, _ => 0 };
                if (flag == 0) break;
                if (flag == 256) nextFlags &= ~32;
                if (flag == 32) nextFlags &= ~256;
                if (flag == 64) nextVerbose = true;
                nextFlags |= flag; end++;
            }
            if (end == offset + 2 || end == pattern.Length || pattern[end] != ')') break;
            flags = nextFlags;
            verbose = nextVerbose;
            offset = end + 1;
        }
        return flags;
    }

    private sealed class Pattern : ILythonRegexPattern
    {
        internal readonly Utf8PythonRegex Regex;
        internal readonly int PatternBytes;
        private readonly ILythonMemoryScope _owner;
        private ILythonMemoryReservation? _replacementCache;
        private readonly object _cacheLock = new();
        public int Flags { get; }
        public int CaptureSlotCount { get; }
        public IReadOnlyDictionary<string, int> NamedGroups { get; }

        internal Pattern(Utf8PythonRegex regex, int flags, int bytes, ILythonMemoryScope owner)
        {
            Regex = regex; Flags = flags; PatternBytes = bytes; _owner = owner;
            var names = regex.GetGroupNames();
            CaptureSlotCount = names.Length;
            var named = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < names.Length; i++) if (!int.TryParse(names[i], out _)) named[names[i]] = i;
            NamedGroups = new ReadOnlyDictionary<string, int>(named);
        }

        internal void FundReplacementCache(int templateBytes)
        {
            // Sixteen retained plans plus unpublished replacement overlap. Keep
            // high-water capacity on the pattern owner, without key tracking.
            var bytes = checked(17L * (512L + 128L * templateBytes));
            lock (_cacheLock)
            {
                if (_replacementCache is null) _replacementCache = Reserve(_owner, bytes);
                else Resize(_replacementCache, bytes);
            }
        }

        public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> subject, ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new Subject(this, subject, memoryOwner, cancellationToken);
        }
    }

    private sealed class Subject : ILythonRegexSubject
    {
        internal readonly Pattern Pattern;
        internal readonly ReadOnlyMemory<byte> Input;
        private readonly ILythonMemoryScope _owner;
        private readonly ILythonMemoryReservation _storage;
        private readonly int[]? _scalarOffsets;
        private readonly long _mapBytes;
        private bool _disposed;

        internal Subject(Pattern pattern, ReadOnlyMemory<byte> input, ILythonMemoryBudget owner, CancellationToken token)
        {
            Pattern = pattern; Input = input;
            _owner = owner.CreateScope();
            try
            {
                var ascii = true;
                for (var offset = 0; offset < input.Length; offset += Math.Min(2048, input.Length - offset))
                {
                    token.ThrowIfCancellationRequested();
                    if (input.Span.Slice(offset, Math.Min(2048, input.Length - offset)).ContainsAnyInRange((byte)128, byte.MaxValue))
                    { ascii = false; break; }
                }
                _mapBytes = ascii ? 0 : checked(32L + 4L * (input.Length + 1L));
                _storage = Reserve(_owner, _mapBytes);
                if (ascii) return;
                _scalarOffsets = new int[checked(input.Length + 1)];
                for (int offset = 0, scalar = 0; offset < input.Length; scalar++)
                {
                    if ((scalar & 1023) == 0) token.ThrowIfCancellationRequested();
                    if (Rune.DecodeFromUtf8(input.Span[offset..], out _, out var width) != OperationStatus.Done)
                        throw new LythonRegexException(LythonRegexFailure.Pattern, "Regex subjects must be valid UTF-8.");
                    offset += width;
                    _scalarOffsets[offset] = scalar + 1;
                }
            }
            catch { _owner.Dispose(); throw; }
        }

        private void Fund(long bytes)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Resize(_storage, checked(_mapBytes + bytes));
        }

        private long MatchBytes => checked(16384L + 128L * Pattern.CaptureSlotCount +
            16L * Input.Length * (Pattern.CaptureSlotCount + 1L) + 16L * Pattern.PatternBytes);

        public LythonRegexMatch? Match(LythonRegexMatchMode mode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Fund(MatchBytes);
            var data = Backend(() => mode switch
            {
                LythonRegexMatchMode.Match => Pattern.Regex.MatchDetailedData(Input.Span, 0, cancellationToken),
                LythonRegexMatchMode.FullMatch => Pattern.Regex.FullMatchDetailedData(Input.Span, 0, cancellationToken),
                _ => Pattern.Regex.SearchDetailedData(Input.Span, 0, cancellationToken),
            });
            return data.Success ? Convert(data, cancellationToken) : null;
        }

        public ILythonRegexCursor Find(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new Cursor(this, cancellationToken);
        }

        public LythonRegexSubstitution Substitute(ReadOnlyMemory<byte> replacement, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matches = count < 0 ? 0 : count == 0 ? 2L * Input.Length + 1 : Math.Min(count, 2L * Input.Length + 1);
            var expansion = replacement.Span.Contains((byte)'\\')
                ? checked((long)replacement.Length * (Input.Length + 2L)) : replacement.Length;
            var output = checked(Input.Length + matches * expansion);
            Fund(checked(MatchBytes + 128L * replacement.Length + 4L * output + 256));
            Pattern.FundReplacementCache(replacement.Length);
            var result = Backend(() => Pattern.Regex.Subn(Input.Span, Encoding.UTF8.GetString(replacement.Span), count, 0, cancellationToken));
            return new(result.ResultBytes, result.ReplacementCount);
        }

        public IReadOnlyList<ReadOnlyMemory<byte>?> Split(int maxSplit, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var maximum = maxSplit < 0 ? 0 : maxSplit == 0 ? 2L * Input.Length + 1 : Math.Min(maxSplit, 2L * Input.Length + 1);
            Fund(checked(128L + 64L * (maximum * Pattern.CaptureSlotCount + 1)));
            var result = new List<ReadOnlyMemory<byte>?>();
            using var cursor = Find(cancellationToken);
            var offset = 0;
            var splits = 0;
            while (maxSplit >= 0 && (maxSplit == 0 || splits < maxSplit) && cursor.Next(cancellationToken) is { } match)
            {
                var whole = match.Captures[0]!.Value;
                var start = whole.StartByte;
                var end = whole.EndByte;
                result.Add(Input[offset..start]);
                for (var i = 1; i < match.Captures.Count; i++)
                { cancellationToken.ThrowIfCancellationRequested(); result.Add(match.Captures[i]?.Value); }
                offset = end;
                splits++;
            }
            result.Add(Input[offset..]);
            return result;
        }

        private LythonRegexMatch Convert(Utf8PythonDetailedMatchData data, CancellationToken token)
        {
            var captures = new LythonRegexCapture?[data.CaptureSlotCount];
            for (var i = 0; i < captures.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!data.TryGetGroup(i, out var group) || !group.Success) continue;
                var start = group.StartOffsetInBytes;
                var end = group.EndOffsetInBytes;
                captures[i] = new(Input[start..end], _scalarOffsets?[start] ?? start, _scalarOffsets?[end] ?? end, start, end);
            }
            return new(captures, data.LastIndex);
        }

        public void Dispose() { if (!_disposed) { _disposed = true; _owner.Dispose(); } }

        private sealed class Cursor : ILythonRegexCursor
        {
            private readonly Subject _subject;
            private readonly ILythonMemoryScope _owner;
            private readonly CancellationTokenSource _cancellation;
            private Utf8PythonMatchCursor? _cursor;
            private bool _disposed;

            internal Cursor(Subject subject, CancellationToken token)
            {
                _subject = subject;
                _owner = subject._owner.CreateScope();
                _cancellation = new CancellationTokenSource();
                try
                {
                    Reserve(_owner, subject.MatchBytes);
                    using var registration = Forward(token);
                    _cursor = Backend(() => subject.Pattern.Regex.CreateMatchCursor(subject.Input, 0, _cancellation.Token));
                }
                catch { Dispose(); throw; }
            }

            private CancellationTokenRegistration Forward(CancellationToken token)
                => token.Register(static state => ((CancellationTokenSource)state!).Cancel(), _cancellation);

            public LythonRegexMatch? Next(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_cursor is null) return null;
                try
                {
                    using (Forward(token))
                    {
                        if (Backend(() => _cursor.MoveNext())) return _subject.Convert(_cursor.Current, token);
                    }
                    Close(); return null;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                { Close(); throw new OperationCanceledException(token); }
                catch { Close(); throw; }
            }

            private void Close()
            {
                _cursor?.Dispose(); _cursor = null;
                _owner.Dispose();
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Close(); _cancellation.Dispose();
            }
        }
    }
}
