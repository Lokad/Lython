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
        private Utf8PythonRegex? _regex;
        internal Utf8PythonRegex Regex => _regex ?? throw new ObjectDisposedException(nameof(Pattern));
        internal readonly int PatternBytes;
        private readonly ILythonMemoryScope _owner;
        private ILythonMemoryReservation? _replacementCache;
        private readonly object _cacheLock = new();
        public int Flags { get; }
        public int CaptureSlotCount { get; }
        private IReadOnlyDictionary<string, int>? _namedGroups;
        public IReadOnlyDictionary<string, int> NamedGroups => _namedGroups ?? throw new ObjectDisposedException(nameof(Pattern));

        internal Pattern(Utf8PythonRegex regex, int flags, int bytes, ILythonMemoryScope owner)
        {
            _regex = regex; Flags = flags; PatternBytes = bytes; _owner = owner;
            var names = regex.GetGroupNames();
            CaptureSlotCount = names.Length;
            var named = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < names.Length; i++) if (!int.TryParse(names[i], out _)) named[names[i]] = i;
            _namedGroups = new ReadOnlyDictionary<string, int>(named);
        }

        internal void FundReplacementCache(int templateBytes)
        {
            // Sixteen retained plans plus unpublished replacement overlap. Keep
            // high-water capacity on the pattern owner, without key tracking.
            var bytes = checked(17L * (512L + 128L * templateBytes));
            lock (_cacheLock)
            {
                ObjectDisposedException.ThrowIf(_regex is null, this);
                if (_replacementCache is null) _replacementCache = Reserve(_owner, bytes);
                else Resize(_replacementCache, bytes);
            }
        }

        public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> subject, LythonRegexWindow window, ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_regex is null, this);
            return new Subject(this, subject, window, memoryOwner, cancellationToken);
        }

        public void Dispose()
        {
            // The caller has drained subjects/cursors and metadata borrows.
            // PythonRe 0.4 owns only managed objects, with no program Dispose API.
            // Drop those references and retire their existing funded scope.
            lock (_cacheLock)
            {
                if (_regex is null) return;
                _regex = null;
                _namedGroups = null;
                _replacementCache = null;
                _owner.Dispose();
            }
        }
    }

    private sealed class Subject : ILythonRegexSubject
    {
        internal readonly Pattern Pattern;
        internal readonly ReadOnlyMemory<byte> Input;
        private readonly ILythonMemoryScope _owner;
        private readonly ILythonMemoryReservation _storage;
        private readonly int _startByte, _endByte;
        private readonly LythonRegexWindow _window;
        private ReadOnlyMemory<byte> BoundedInput => Input[.._endByte];
        private bool _disposed;

        internal Subject(Pattern pattern, ReadOnlyMemory<byte> input, LythonRegexWindow window, ILythonMemoryBudget owner, CancellationToken token)
        {
            Pattern = pattern; Input = input; _window = window;
            _startByte = window.StartByte; _endByte = window.EndByte;
            _owner = owner.CreateScope();
            try
            {
                token.ThrowIfCancellationRequested();
                _storage = Reserve(_owner, 0);
                ValidateWindow();
            }
            catch { _owner.Dispose(); throw; }
        }

        private void ValidateWindow()
        {
            if (_window.Pos < 0 || _window.EndPos < 0 ||
                (uint)_startByte > (uint)Input.Length || (uint)_endByte > (uint)Input.Length ||
                _startByte < Input.Length && (Input.Span[_startByte] & 0xc0) == 0x80 ||
                _endByte < Input.Length && (Input.Span[_endByte] & 0xc0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(_window), "Regex window bounds must be clamped to the original scalar length.");
        }

        private void Fund(long bytes)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Resize(_storage, bytes);
        }

        private long MatchBytes => checked(16384L + 128L * Pattern.CaptureSlotCount +
            16L * Input.Length * (Pattern.CaptureSlotCount + 1L) + 16L * Pattern.PatternBytes);

        public LythonRegexMatch? Match(LythonRegexMatchMode mode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_window.Pos > _window.EndPos)
            {
                if (mode != LythonRegexMatchMode.Match) return null;
                throw new LythonRegexException(LythonRegexFailure.Capability,
                    "PythonRe 0.4 does not support Python match with pos greater than endpos.");
            }
            Fund(MatchBytes);
            var data = Backend(() => mode switch
            {
                LythonRegexMatchMode.Match => Pattern.Regex.MatchDetailedData(BoundedInput.Span, _startByte, cancellationToken),
                LythonRegexMatchMode.FullMatch => Pattern.Regex.FullMatchDetailedData(BoundedInput.Span, _startByte, cancellationToken),
                _ => Pattern.Regex.SearchDetailedData(BoundedInput.Span, _startByte, cancellationToken),
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
            var result = Backend(() => Pattern.Regex.Subn(BoundedInput.Span, Encoding.UTF8.GetString(replacement.Span), count, _startByte, cancellationToken));
            return new(result.ResultBytes.AsMemory(_startByte), result.ReplacementCount);
        }

        public IReadOnlyList<ReadOnlyMemory<byte>?> Split(int maxSplit, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_window.Pos > _window.EndPos) return new ReadOnlyMemory<byte>?[] { ReadOnlyMemory<byte>.Empty };
            var maximum = maxSplit < 0 ? 0 : maxSplit == 0 ? 2L * Input.Length + 1 : Math.Min(maxSplit, 2L * Input.Length + 1);
            Fund(checked(128L + 64L * (maximum * Pattern.CaptureSlotCount + 1)));
            var result = new List<ReadOnlyMemory<byte>?>();
            using var cursor = Find(cancellationToken);
            var offset = _startByte;
            var splits = 0;
            while (maxSplit >= 0 && (maxSplit == 0 || splits < maxSplit) && cursor.Next(cancellationToken) is { } match)
            {
                var whole = match.Captures[0]!.Value;
                var start = whole.StartByte;
                var end = whole.EndByte;
                result.Add(Input[offset..start]);
                for (var i = 1; i < match.Captures.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.Add(match.Captures[i] is { } capture
                        ? Input[capture.StartByte..capture.EndByte] : (ReadOnlyMemory<byte>?)null);
                }
                offset = end;
                splits++;
            }
            result.Add(Input[offset.._endByte]);
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
                captures[i] = new(start, end);
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
                    if (subject._window.Pos > subject._window.EndPos) return;
                    Reserve(_owner, subject.MatchBytes);
                    using var registration = Forward(token);
                    _cursor = Backend(() => subject.Pattern.Regex.CreateMatchCursor(subject.BoundedInput, subject._startByte, _cancellation.Token));
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
