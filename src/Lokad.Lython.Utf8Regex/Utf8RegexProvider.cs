using System.Buffers;
using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Utf8Regex;

/// <summary>Optional PythonRe adapter with borrowed UTF-8 subjects and explicit storage owners.</summary>
public sealed class Utf8RegexProvider : ILythonRegexProvider
{
    private const int LocaleFlag = 4;
    private const int UnicodeFlag = 32;
    private const int AsciiFlag = 256;
    private const string ConflictingModes = "ASCII and UNICODE flags are incompatible";

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
            var matchingFlags = options.Flags;
            if (options.ProgramFlags is { } programFlags)
            {
                if ((programFlags & LocaleFlag) != 0)
                    throw new LythonRegexException(LythonRegexFailure.Capability,
                        "The regex provider requires culture-invariant matching.");
                // The producer defaults to Unicode. An explicit compiler
                // request without the Unicode bit uses ASCII character modes.
                matchingFlags = (programFlags & UnicodeFlag) != 0 ? programFlags & ~AsciiFlag : programFlags | AsciiFlag;
            }
            var backendOptions = Options(matchingFlags);
            backendOptions = options.Verbose ? backendOptions | PythonReCompileOptions.Verbose : backendOptions & ~PythonReCompileOptions.Verbose;
            var policy = new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ConstructionTimeout = Timeout.InfiniteTimeSpan,
                ProgramMemoryAllowance = new RegexAllowance(owner),
            };
            var regex = Backend((Pattern: pattern, Options: backendOptions, Policy: policy), static (state, token) =>
                Utf8PythonRegex.Compile(state.Pattern.Span, state.Options, state.Policy, token),
                cancellationToken, utf8Pattern: pattern);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new Pattern(regex, EffectiveFlags(pattern.Span, options, cancellationToken), pattern.Length, owner, cancellationToken);
            }
            catch { regex.Dispose(); throw; }
        }
        catch { owner.Dispose(); throw; }
    }

    // Native UTF-8 producers may decode Pattern on access. Read that text
    // only after an error, without allocating a deferred delegate per match.
    private static T Backend<TState, T>(TState state, Func<TState, CancellationToken, T> work,
        CancellationToken token, ReadOnlyMemory<byte> utf8Pattern = default, Utf8PythonRegex? patternSource = null)
    {
        try { return work(state, token); }
        catch (Utf8MemoryAdmissionException error)
        {
            throw new LythonRegexException(LythonRegexFailure.MemoryAdmission,
                "Shared memory budget refused regex storage.", requestedBytes: (long)Math.Min(error.RequestedBytes, long.MaxValue));
        }
        catch (PythonRePatternException error)
        {
            token.ThrowIfCancellationRequested();
            // An unpositioned global mode conflict is a Python ValueError.
            // Scoped conflicts retain their pattern positions.
            if (error.Position < 0 && error.Message == ConflictingModes)
                throw new LythonRegexException(LythonRegexFailure.Flags, ConflictingModes);
            throw new LythonRegexException(LythonRegexFailure.Pattern, error.Message,
                ScalarErrorPosition(error.Position, utf8Pattern, patternSource?.Pattern, token));
        }
        catch (ArgumentException error)
        { throw new LythonRegexException(LythonRegexFailure.Pattern, error.Message); }
    }

    // PythonRe reports UTF-16 offsets or -1. Normalize only on failure,
    // borrowing compilation UTF-8 or the backend's existing original string.
    private static int? ScalarErrorPosition(int position, ReadOnlyMemory<byte> utf8Pattern,
        string? utf16Pattern, CancellationToken token)
    {
        if (position < 0) return null;
        var units = 0;
        var scalars = 0;
        for (var offset = 0; units < position;)
        {
            token.ThrowIfCancellationRequested();
            Rune rune;
            int consumed;
            var status = utf16Pattern is null
                ? Rune.DecodeFromUtf8(utf8Pattern.Span[offset..], out rune, out consumed)
                : Rune.DecodeFromUtf16(utf16Pattern.AsSpan(offset), out rune, out consumed);
            if (status != System.Buffers.OperationStatus.Done) return null;
            if (units + rune.Utf16SequenceLength > position) return scalars;
            units += rune.Utf16SequenceLength;
            offset += consumed;
            scalars++;
        }
        return scalars;
    }

    private static ILythonMemoryReservation Reserve(ILythonMemoryBudget owner, long bytes)
    {
        if (!owner.TryReserve(bytes, out var storage))
            throw new LythonRegexException(LythonRegexFailure.MemoryAdmission,
                "Shared memory budget refused regex storage.", requestedBytes: bytes);
        return storage;
    }

    // Aggregate the producer's live claims in one reservation per owner. A
    // partially released reservation leaves reusable capacity for that owner.
    // The final producer release returns the full reservation synchronously.
    private sealed class RegexAllowance(ILythonMemoryBudget owner) : IUtf8MemoryAllowance
    {
        private readonly object _gate = new();
        private ILythonMemoryReservation? _storage;
        private long _liveBytes;

        public bool TryReserve(ulong bytes)
        {
            if (bytes == 0) return true;
            lock (_gate)
            {
                if (bytes > (ulong)(long.MaxValue - _liveBytes)) return false;
                var total = _liveBytes + (long)bytes;
                if (_storage is { } storage)
                {
                    if (total > storage.Bytes && !storage.TryResize(total)) return false;
                }
                else
                {
                    if (!owner.TryReserve(total, out var admitted)) return false;
                    _storage = admitted;
                }
                _liveBytes = total;
                return true;
            }
        }

        public void Release(ulong bytes)
        {
            lock (_gate)
            {
                System.Diagnostics.Debug.Assert(bytes <= (ulong)_liveBytes);
                _liveBytes -= (long)bytes;
                if (_liveBytes != 0) return;
                var storage = _storage;
                _storage = null;
                storage?.Dispose();
            }
        }
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

    private static int EffectiveFlags(ReadOnlySpan<byte> pattern, LythonRegexOptions options, CancellationToken token)
    {
        // Reported Python flags can differ from dispatched parsing flags.
        // Read the validated global prefix directly from the original UTF-8.
        var flags = options.ImplicitUnicode ? options.Flags & ~UnicodeFlag : options.Flags;
        var verbose = options.Verbose;
        var offset = 0;
        while (offset < pattern.Length)
        {
            token.ThrowIfCancellationRequested();
            if (verbose && pattern[offset] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\v' or (byte)'\f') { offset++; continue; }
            if (verbose && pattern[offset] == (byte)'#')
            {
                while (offset < pattern.Length && pattern[offset] != (byte)'\n')
                {
                    token.ThrowIfCancellationRequested();
                    offset += pattern[offset] == (byte)'\\' && offset + 1 < pattern.Length ? 2 : 1;
                }
                continue;
            }
            if (pattern[offset..].StartsWith("(?#"u8))
            {
                offset += 3;
                while (offset < pattern.Length && pattern[offset] != (byte)')')
                {
                    token.ThrowIfCancellationRequested();
                    offset += pattern[offset] == (byte)'\\' && offset + 1 < pattern.Length ? 2 : 1;
                }
                offset++; continue;
            }
            if (!pattern[offset..].StartsWith("(?"u8)) break;
            var end = offset + 2;
            var nextFlags = flags;
            var nextVerbose = verbose;
            while (end < pattern.Length)
            {
                token.ThrowIfCancellationRequested();
                var flag = pattern[end] switch { (byte)'i' => 2, (byte)'m' => 8, (byte)'s' => 16, (byte)'x' => 64, (byte)'a' => AsciiFlag, (byte)'u' => UnicodeFlag, _ => 0 };
                if (flag == 0) break;
                if (flag == 64) nextVerbose = true;
                nextFlags |= flag; end++;
            }
            if (end == offset + 2 || end == pattern.Length || pattern[end] != (byte)')') break;
            flags = nextFlags;
            verbose = nextVerbose;
            offset = end + 1;
        }
        if ((flags & LocaleFlag) != 0)
            throw new LythonRegexException(LythonRegexFailure.Flags, "cannot use LOCALE flag with a str pattern");
        var mode = flags & (AsciiFlag | UnicodeFlag);
        if (mode == (AsciiFlag | UnicodeFlag))
            throw new LythonRegexException(LythonRegexFailure.Flags, ConflictingModes);
        return options.ImplicitUnicode && mode == 0 ? flags | UnicodeFlag : flags;
    }

    private sealed class Pattern : ILythonRegexPattern
    {
        private Utf8PythonRegex? _regex;
        internal Utf8PythonRegex Regex => _regex ?? throw new ObjectDisposedException(nameof(Pattern));
        private readonly ILythonMemoryScope _owner;
        public int Flags { get; }
        public int CaptureSlotCount { get; }
        private IReadOnlyList<LythonRegexNamedGroup>? _namedGroups;
        public IReadOnlyList<LythonRegexNamedGroup> NamedGroups => _namedGroups ?? throw new ObjectDisposedException(nameof(Pattern));

        internal Pattern(Utf8PythonRegex regex, int flags, int bytes, ILythonMemoryScope owner, CancellationToken token)
        {
            _regex = regex; Flags = flags; _owner = owner;
            CaptureSlotCount = checked(regex.CaptureGroupCount + 1);
            Reserve(owner, checked(256L + 64L * CaptureSlotCount + bytes));
            token.ThrowIfCancellationRequested();
            var named = new List<LythonRegexNamedGroup>();
            Span<byte> scratch = stackalloc byte[64];
            for (var i = 1; i < CaptureSlotCount; i++)
            {
                var status = regex.TryCopyGroupNameUtf8(i, scratch, out var length, token);
                if (status == OperationStatus.Done)
                {
                    // Unnamed slots use ASCII decimal indices.
                    if (scratch[0] is < (byte)'0' or > (byte)'9')
                        named.Add(new(scratch[..length].ToArray(), i));
                }
                else if (status == OperationStatus.DestinationTooSmall)
                {
                    var name = new byte[length];
                    if (regex.TryCopyGroupNameUtf8(i, name, out _, token) != OperationStatus.Done)
                        throw new InvalidOperationException("Regex group name changed during publication.");
                    named.Add(new(name, i));
                }
                else throw new InvalidOperationException("Regex group name is not valid UTF-8.");
            }
            _namedGroups = named.AsReadOnly();
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
            if (Interlocked.Exchange(ref _regex, null) is not { } regex) return;
            try { regex.Dispose(); }
            finally { _namedGroups = null; _owner.Dispose(); }
        }
    }

    private sealed class Subject : ILythonRegexSubject
    {
        internal readonly Pattern Pattern;
        private ReadOnlyMemory<byte> _input;
        private readonly ILythonMemoryBudget _budget;
        private readonly LythonRegexWindow _window;
        private bool _disposed;

        internal Subject(Pattern pattern, ReadOnlyMemory<byte> input, LythonRegexWindow window, ILythonMemoryBudget owner, CancellationToken token)
        {
            Pattern = pattern; _input = input; _window = window; _budget = owner;
            token.ThrowIfCancellationRequested();
            ValidateWindow();
        }

        private void ValidateWindow()
        {
            var start = _window.StartByte;
            var end = _window.EndByte;
            if (_window.Pos < 0 || _window.EndPos < 0 ||
                (uint)start > (uint)_input.Length || (uint)end > (uint)_input.Length ||
                start < _input.Length && (_input.Span[start] & 0xc0) == 0x80 ||
                end < _input.Length && (_input.Span[end] & 0xc0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(_window), "Regex window bounds must be clamped to the original scalar length.");
        }

        public bool TryMatch(LythonRegexMatchMode mode, Span<LythonRegexCapture?> captures, out int? lastIndex,
            CancellationToken cancellationToken)
        {
            lastIndex = null;
            if (captures.Length != Pattern.CaptureSlotCount) throw new ArgumentException("Capture buffer length must equal CaptureSlotCount.", nameof(captures));
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_window.Pos > _window.EndPos)
            {
                if (mode != LythonRegexMatchMode.Match) return false;
                throw new LythonRegexException(LythonRegexFailure.Capability,
                    "PythonRe 0.5 does not support Python match with pos greater than endpos.");
            }
            var regex = Pattern.Regex;
            var backendMode = mode switch
            {
                LythonRegexMatchMode.Match => Utf8PythonMatchMode.Match,
                LythonRegexMatchMode.FullMatch => Utf8PythonMatchMode.FullMatch,
                _ => Utf8PythonMatchMode.Search,
            };
            using var owner = _budget.CreateScope();
            var allowance = new RegexAllowance(owner);
            if (captures.Length <= 2)
            {
                // With one subgroup, participation determines lastindex.
                // The backend writes into slots scoped to this call.
                var bounds = Backend((Regex: regex, Input: _input, Window: _window, Mode: backendMode,
                    Allowance: allowance, HasGroup: captures.Length == 2),
                    static (state, token) =>
                    {
                        Span<Utf8PythonCaptureRange> ranges = stackalloc Utf8PythonCaptureRange[2];
                        var match = state.Regex.Match(state.Input.Span, state.Window.StartByte,
                            state.HasGroup ? ranges : default,
                            cancellationToken: token, workingMemoryAllowance: state.Allowance, mode: state.Mode,
                            endOffsetInBytes: state.Window.EndByte);
                        return (match.Success, match.StartOffsetInBytes, match.EndOffsetInBytes,
                            Group: match.Success && state.HasGroup ? ranges[1] : default);
                    }, cancellationToken, patternSource: regex);
                cancellationToken.ThrowIfCancellationRequested();
                if (!bounds.Success) return false;
                captures[0] = new(bounds.StartOffsetInBytes, bounds.EndOffsetInBytes);
                if (captures.Length == 2)
                {
                    captures[1] = bounds.Group.Success
                        ? new(bounds.Group.StartOffsetInBytes, bounds.Group.EndOffsetInBytes) : null;
                    if (bounds.Group.Success) lastIndex = 1;
                }
                return true;
            }
            // The one-shot range writer lacks last-close metadata. Borrow the
            // producer's funded range result only while copying it.
            using var cursor = Backend((Regex: regex, Input: _input, Window: _window, Mode: backendMode, Allowance: allowance),
                static (state, token) => state.Regex.CreateMatchCursor(state.Input, state.Window.StartByte,
                    token, state.Allowance, mode: state.Mode, endOffsetInBytes: state.Window.EndByte),
                cancellationToken, patternSource: regex);
            using var match = Backend(cursor, static (current, token) => current.MoveNextRetained(out var next, token) ? next : null,
                cancellationToken, patternSource: regex);
            if (match is null) return false;
            var data = match.Match;
            CopyCaptures(data, captures, cancellationToken);
            lastIndex = data.LastIndex;
            return true;
        }

        public ILythonRegexCursor Find(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new Cursor(this, cancellationToken);
        }

        private static void CopyCaptures(Utf8PythonRangeMatch data, Span<LythonRegexCapture?> captures, CancellationToken token)
        {
            for (var i = 0; i < captures.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!data.TryGetGroup(i, out var group) || !group.Success) { captures[i] = null; continue; }
                var start = group.StartOffsetInBytes;
                var end = group.EndOffsetInBytes;
                captures[i] = new(start, end);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _input = default;
        }

        private sealed class Cursor : ILythonRegexCursor
        {
            private readonly Subject _subject;
            private readonly ILythonMemoryScope _owner;
            private Utf8PythonMatchCursor? _cursor;
            private bool _disposed;

            internal Cursor(Subject subject, CancellationToken token)
            {
                _subject = subject;
                _owner = subject._budget.CreateScope();
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (subject._window.Pos > subject._window.EndPos) return;
                    var regex = subject.Pattern.Regex;
                    // Forward only construction. Later advances supply their
                    // own token through the producer's per-advance overload.
                    using var initialization = new CancellationTokenSource();
                    using var forwarding = token.Register(static state => ((CancellationTokenSource)state!).Cancel(), initialization);
                    _cursor = Backend((Regex: regex, Input: subject._input, Window: subject._window, Token: initialization.Token,
                        Allowance: new RegexAllowance(_owner)),
                        static (state, _) => state.Regex.CreateMatchCursor(state.Input, state.Window.StartByte,
                            state.Token, state.Allowance, endOffsetInBytes: state.Window.EndByte),
                        token, patternSource: regex);
                    token.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                { Dispose(); throw new OperationCanceledException(token); }
                catch { Dispose(); throw; }
            }

            public bool TryNext(Span<LythonRegexCapture?> captures, out int? lastIndex, CancellationToken token)
            {
                lastIndex = null;
                if (captures.Length != _subject.Pattern.CaptureSlotCount) throw new ArgumentException("Capture buffer length must equal CaptureSlotCount.", nameof(captures));
                token.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_cursor is not { } cursor) return false;
                try
                {
                    using var match = Backend(cursor, static (current, ct) => current.MoveNextRetained(out var next, ct) ? next : null,
                        token, patternSource: _subject.Pattern.Regex);
                    if (match is null) { Close(); return false; }
                    var data = match.Match;
                    CopyCaptures(data, captures, token);
                    lastIndex = data.LastIndex;
                    return true;
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
                Close();
            }
        }
    }
}
