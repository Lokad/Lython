using System.Buffers;
using System.Collections.ObjectModel;
using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Utf8Regex;

/// <summary>Optional PythonRe adapter. See its README for backend cancellation and capture limitations.</summary>
public sealed class Utf8RegexProvider : ILythonRegexProvider
{
    /// <summary>Gets a stateless provider instance; patterns and working owners remain caller-local.</summary>
    public static Utf8RegexProvider Instance { get; } = new();

    /// <inheritdoc />
    public ILythonRegexPattern Compile(ReadOnlyMemory<byte> pattern, LythonRegexOptions options,
        ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
    {
        RequireBackendCancellation(cancellationToken);
        var owner = memoryOwner.CreateScope();
        try
        {
            // Major retained compiler tables plus construction overlap. Count
            // possible slots before compilation; escaped parentheses may overpay.
            var slots = 1;
            foreach (var value in pattern.Span) if (value == (byte)'(') slots++;
            Reserve(owner, checked(65536L + 2048L * Math.Max(0, slots - 2) + 256L * Math.Max(0, pattern.Length - 64)));
            var backendOptions = Options(options.Flags);
            backendOptions = options.Verbose ? backendOptions | PythonReCompileOptions.Verbose : backendOptions & ~PythonReCompileOptions.Verbose;
            var regex = Backend(() => new Utf8PythonRegex(pattern.Span, backendOptions, Timeout.InfiniteTimeSpan));
            return new Pattern(regex, EffectiveFlags(regex.Pattern, options.Flags), pattern.Length,
                pattern.Span.SequenceEqual(".*?"u8));
        }
        catch { owner.Dispose(); throw; }
    }

    private static void RequireBackendCancellation(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (token.CanBeCanceled)
            throw new LythonRegexException(LythonRegexFailure.Capability,
                "This Utf8Regex adapter cannot interrupt backend work with CancellationToken. Supply a provider with in-flight cancellation support.");
    }

    private static T Backend<T>(Func<T> work)
    {
        try { return work(); }
        catch (PythonRePatternException error)
        { throw new LythonRegexException(LythonRegexFailure.Pattern, error.Message, error.Position); }
        catch (ArgumentException error)
        { throw new LythonRegexException(LythonRegexFailure.Pattern, error.Message); }
    }

    private static void Reserve(ILythonMemoryBudget owner, long bytes)
    {
        if (!owner.TryReserve(bytes, out _))
            throw new LythonRegexException(LythonRegexFailure.MemoryAdmission,
                "Shared memory budget refused regex storage.", requestedBytes: bytes);
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

    private static int EffectiveFlags(string pattern, int flags)
    {
        var offset = 0;
        while (offset + 3 < pattern.Length && pattern[offset] == '(' && pattern[offset + 1] == '?')
        {
            var end = pattern.IndexOf(')', offset + 2);
            if (end < 0) break;
            var valid = true;
            var nextFlags = flags;
            for (var i = offset + 2; i < end; i++)
            {
                var flag = pattern[i] switch { 'i' => 2, 'm' => 8, 's' => 16, 'x' => 64, 'a' => 256, 'u' => 32, _ => 0 };
                if (flag == 0) { valid = false; break; }
                if (flag == 256) nextFlags &= ~32;
                if (flag == 32) nextFlags &= ~256;
                nextFlags |= flag;
            }
            if (!valid) break;
            flags = nextFlags;
            offset = end + 1;
        }
        return flags;
    }

    private sealed class Pattern : ILythonRegexPattern
    {
        internal readonly Utf8PythonRegex Regex;
        internal readonly int PatternBytes;
        internal readonly bool DotStarLazy;
        public int Flags { get; }
        public int CaptureSlotCount { get; }
        public IReadOnlyDictionary<string, int> NamedGroups { get; }

        internal Pattern(Utf8PythonRegex regex, int flags, int bytes, bool dotStarLazy)
        {
            Regex = regex; Flags = flags; PatternBytes = bytes; DotStarLazy = dotStarLazy;
            var names = regex.GetGroupNames();
            CaptureSlotCount = names.Length;
            var named = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < names.Length; i++) if (!int.TryParse(names[i], out _)) named[names[i]] = i;
            NamedGroups = new ReadOnlyDictionary<string, int>(named);
        }

        public ILythonRegexSubject Prepare(ReadOnlyMemory<byte> subject, ILythonMemoryBudget memoryOwner, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new Subject(this, subject, memoryOwner);
        }
    }

    private sealed class Subject : ILythonRegexSubject
    {
        internal readonly Pattern Pattern;
        internal readonly ReadOnlyMemory<byte> Input;
        private readonly ILythonMemoryScope _owner;
        private readonly ILythonMemoryReservation _storage;
        private bool _disposed;

        internal Subject(Pattern pattern, ReadOnlyMemory<byte> input, ILythonMemoryBudget owner)
        {
            Pattern = pattern; Input = input;
            _owner = owner.CreateScope();
            if (!_owner.TryReserve(0, out var storage))
            {
                _owner.Dispose();
                throw new LythonRegexException(LythonRegexFailure.MemoryAdmission, "Shared memory budget refused regex preparation.", requestedBytes: 0);
            }
            _storage = storage;
        }

        private void Fund(long bytes)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            bytes = Math.Max(bytes, _storage.Bytes);
            if (!_storage.TryResize(bytes))
                throw new LythonRegexException(LythonRegexFailure.MemoryAdmission,
                    "Shared memory budget refused regex working storage.", requestedBytes: Math.Max(0, bytes - _storage.Bytes));
        }

        private long MatchBytes => checked(4096L + Pattern.CaptureSlotCount * (128L + 4L * Input.Length + 2L * Pattern.PatternBytes));
        private long AllMatchBytes => checked(MatchBytes * (2L * Input.Length + 1));

        public LythonRegexMatch? Match(LythonRegexMatchMode mode, CancellationToken cancellationToken)
        {
            RequireBackendCancellation(cancellationToken);
            Fund(MatchBytes);
            var data = Backend(() => mode switch
            {
                LythonRegexMatchMode.Match => Pattern.Regex.MatchDetailedData(Input.Span),
                LythonRegexMatchMode.FullMatch => Pattern.Regex.FullMatchDetailedData(Input.Span),
                _ => Pattern.Regex.SearchDetailedData(Input.Span),
            });
            return data.Success ? Convert(data, cancellationToken) : null;
        }

        public ILythonRegexCursor Find(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Pattern.DotStarLazy) { Fund(4096); return new LazyDotCursor(this); }
            RequireBackendCancellation(cancellationToken);
            RequireBufferedUnicode();
            Fund(AllMatchBytes);
            return new Cursor(this, Backend(() => Pattern.Regex.FindIterDetailed(Input.Span)));
        }

        public LythonRegexSubstitution Substitute(ReadOnlyMemory<byte> replacement, int count, CancellationToken cancellationToken)
        {
            RequireBackendCancellation(cancellationToken);
            if (!Pattern.DotStarLazy) RequireBufferedUnicode();
            var matches = count < 0 ? 0 : count == 0 ? 2L * Input.Length + 1 : Math.Min(count, 2L * Input.Length + 1);
            var expansion = replacement.Span.Contains((byte)'\\')
                ? checked((long)replacement.Length * (Input.Length + 2L)) : replacement.Length;
            var output = checked(Input.Length + matches * expansion);
            Fund(checked((Pattern.DotStarLazy ? 4096 : AllMatchBytes) + 128L * replacement.Length + 4L * output + 256));
            if (Pattern.DotStarLazy) return SubstituteLazy(replacement, count, cancellationToken);
            var result = Backend(() => Pattern.Regex.Subn(Input.Span, Encoding.UTF8.GetString(replacement.Span), count));
            return new(result.ResultBytes, result.ReplacementCount);
        }

        public IReadOnlyList<ReadOnlyMemory<byte>?> Split(int maxSplit, CancellationToken cancellationToken)
        {
            if (Pattern.DotStarLazy) Fund(checked(4096L + 128L * (2L * Input.Length + 2)));
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
                for (var i = 1; i < match.Captures.Count; i++) result.Add(match.Captures[i]?.Value);
                offset = end;
                splits++;
            }
            result.Add(Input[offset..]);
            return result;
        }

        private int ScalarOffset(int offset)
        {
            var position = 0;
            var scalar = 0;
            while (position < offset && position < Input.Length)
            {
                Rune.DecodeFromUtf8(Input.Span[position..], out _, out var width);
                position += width; scalar++;
            }
            return scalar;
        }

        private int ByteOffset(int scalar)
        {
            var offset = 0;
            while (scalar-- > 0)
            {
                if (offset >= Input.Length)
                    throw new LythonRegexException(LythonRegexFailure.Capability, "Backend capture offsets exceed the UTF-8 subject.");
                Rune.DecodeFromUtf8(Input.Span[offset..], out _, out var width); offset += width;
            }
            return offset;
        }

        private void RequireBufferedUnicode()
        {
            var scalars = 0;
            var supplementary = false;
            for (var offset = 0; offset < Input.Length; scalars++)
            {
                Rune.DecodeFromUtf8(Input.Span[offset..], out var rune, out var width);
                supplementary |= rune.Value > 0xffff;
                offset += width;
            }
            if (supplementary && scalars > 1)
                throw new LythonRegexException(LythonRegexFailure.Capability,
                    "This backend's buffered matching does not reliably support multi-scalar subjects containing supplementary Unicode. Supply a provider with that capability.");
        }

        private LythonRegexMatch Convert(Utf8PythonDetailedMatchData data, CancellationToken token)
        {
            var captures = new LythonRegexCapture?[data.CaptureSlotCount];
            int? last = null;
            for (var i = 0; i < captures.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!data.TryGetGroup(i, out var group) || !group.Success) continue;
                var start = group.HasContiguousByteRange ? ScalarOffset(group.StartOffsetInBytes) : group.StartOffsetInUtf16;
                var end = group.HasContiguousByteRange ? ScalarOffset(group.EndOffsetInBytes) : group.EndOffsetInUtf16;
                // The 0.2 fallback labels scalar positions as UTF-16 positions.
                // Preserve the existing one-shot projection, validate it against
                // the returned text, and keep normal byte ranges as borrowed views.
                var startByte = group.HasContiguousByteRange ? group.StartOffsetInBytes : ByteOffset(start);
                var endByte = group.HasContiguousByteRange ? group.EndOffsetInBytes : ByteOffset(end);
                ReadOnlyMemory<byte> value = Input[startByte..endByte];
                if (!group.HasContiguousByteRange && !value.Span.SequenceEqual(Encoding.UTF8.GetBytes(group.ValueText)))
                    throw new LythonRegexException(LythonRegexFailure.Capability, "Backend capture coordinates do not describe the original UTF-8 subject.");
                captures[i] = new(value, start, end, startByte, endByte);
                if (i > 0) last = i; // The immutable backend does not expose capture-close order.
            }
            return new(captures, last);
        }

        public void Dispose() { if (!_disposed) { _disposed = true; _owner.Dispose(); } }

        private sealed class Cursor(Subject subject, Utf8PythonDetailedMatchData[] matches) : ILythonRegexCursor
        {
            private Utf8PythonDetailedMatchData[]? _matches = matches;
            private int _index;
            public LythonRegexMatch? Next(CancellationToken token)
            {
                RequireBackendCancellation(token);
                ObjectDisposedException.ThrowIf(_matches is null, this);
                return _index == _matches.Length ? null : subject.Convert(_matches[_index++], token);
            }
            public void Dispose() => _matches = null;
        }

        private sealed class LazyDotCursor(Subject subject) : ILythonRegexCursor
        {
            private int _byte;
            private int _scalar;
            private bool _character;
            private bool _finished;
            public LythonRegexMatch? Next(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (_finished) return null;
                if (!_character) { _character = true; return new([new(subject.Input.Slice(_byte, 0), _scalar, _scalar, _byte, _byte)], null); }
                if (_byte == subject.Input.Length) { _finished = true; return null; }
                Rune.DecodeFromUtf8(subject.Input.Span[_byte..], out var rune, out var width);
                var value = subject.Input.Slice(_byte, width);
                _byte += width; _scalar++; _character = false;
                if (rune.Value == '\n' && (subject.Pattern.Flags & 16) == 0) return Next(token);
                return new([new(value, _scalar - 1, _scalar, _byte - width, _byte)], null);
            }
            public void Dispose() => _finished = true;
        }

        private LythonRegexSubstitution SubstituteLazy(ReadOnlyMemory<byte> template, int count, CancellationToken token)
        {
            // Preserve the existing Python empty/nonempty progression workaround
            // in the optional adapter, with no backend calls during enumeration.
            var output = new ArrayBufferWriter<byte>();
            Expand(template.Span, ReadOnlySpan<byte>.Empty, new ArrayBufferWriter<byte>());
            using var cursor = new LazyDotCursor(this);
            var offset = 0;
            var replacements = 0;
            while (count >= 0 && (count == 0 || replacements < count) && cursor.Next(token) is { } match)
            {
                var whole = match.Captures[0]!.Value;
                var start = whole.StartByte;
                var end = whole.EndByte;
                output.Write(Input.Span[offset..start]);
                Expand(template.Span, whole.Value.Span, output);
                offset = end; replacements++;
            }
            output.Write(Input.Span[offset..]);
            return new(output.WrittenSpan.ToArray(), replacements);
        }

        private static void Expand(ReadOnlySpan<byte> text, ReadOnlySpan<byte> whole, ArrayBufferWriter<byte> output)
        {
            Span<byte> encoded = stackalloc byte[4];
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '\\') { output.Write(text.Slice(i, 1)); continue; }
                if (++i == text.Length) throw new LythonRegexException(LythonRegexFailure.Pattern, "bad escape (end of pattern)");
                var ch = text[i];
                if (ch == 'g' && i + 1 < text.Length && text[i + 1] == '<')
                {
                    var end = text[(i + 2)..].IndexOf((byte)'>');
                    if (end < 0) throw new LythonRegexException(LythonRegexFailure.Pattern, "missing > in group reference");
                    var name = Encoding.UTF8.GetString(text.Slice(i + 2, end));
                    if (!int.TryParse(name, out var group) || group != 0)
                        throw new LythonRegexException(LythonRegexFailure.Pattern, "invalid group reference");
                    output.Write(whole); i += end + 2; continue;
                }
                if (ch is >= (byte)'0' and <= (byte)'7' &&
                    (ch == '0' || i + 2 < text.Length && text[i + 1] is >= (byte)'0' and <= (byte)'7' && text[i + 2] is >= (byte)'0' and <= (byte)'7'))
                {
                    var value = ch - '0';
                    var digits = 1;
                    while (digits < 3 && i + 1 < text.Length && text[i + 1] is >= (byte)'0' and <= (byte)'7')
                    { value = value * 8 + text[++i] - '0'; digits++; }
                    if (value > 255) throw new LythonRegexException(LythonRegexFailure.Pattern, "octal escape value outside of range 0-0o377");
                    output.Write(encoded[..new Rune(value).EncodeToUtf8(encoded)]); continue;
                }
                if (ch is >= (byte)'0' and <= (byte)'9') throw new LythonRegexException(LythonRegexFailure.Pattern, "invalid group reference");
                var escaped = ch switch { (byte)'a' => 7, (byte)'b' => 8, (byte)'f' => 12, (byte)'n' => 10, (byte)'r' => 13, (byte)'t' => 9, (byte)'v' => 11, (byte)'\\' => 92, _ => -1 };
                if (escaped >= 0) { encoded[0] = (byte)escaped; output.Write(encoded[..1]); continue; }
                if (ch is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z')
                    throw new LythonRegexException(LythonRegexFailure.Pattern, "bad escape in replacement");
                output.Write("\\"u8); output.Write(text.Slice(i, 1));
            }
        }
    }
}
