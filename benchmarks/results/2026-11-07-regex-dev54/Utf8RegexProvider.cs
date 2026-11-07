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
                Timeout.InfiniteTimeSpan, cancellationToken), cancellationToken, utf8Pattern: pattern);
            cancellationToken.ThrowIfCancellationRequested();
            return new Pattern(regex, EffectiveFlags(pattern.Span, options, cancellationToken), pattern.Length, owner, cancellationToken);
        }
        catch { owner.Dispose(); throw; }
    }

    // Native UTF-8 producers may decode Pattern on access. Read that text
    // only after an error, without allocating a deferred delegate per match.
    private static T Backend<T>(Func<T> work, CancellationToken token, ReadOnlyMemory<byte> utf8Pattern = default, Utf8PythonRegex? patternSource = null)
    {
        try { return work(); }
        catch (PythonRePatternException error)
        {
            token.ThrowIfCancellationRequested();
            throw new LythonRegexException(LythonRegexFailure.Pattern, error.Message,
                ScalarErrorPosition(error.Position, utf8Pattern, patternSource?.Pattern, token));
        }
        catch (ArgumentException error)
        { throw new LythonRegexException(LythonRegexFailure.Pattern, error.Message); }
    }

    // PythonRe 0.4 reports UTF-16 offsets/-1. Normalize only on failure,
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

    private static int EffectiveFlags(ReadOnlySpan<byte> pattern, LythonRegexOptions options, CancellationToken token)
    {
        // Reported Python flags can differ from dispatched parsing flags.
        // Read the validated global prefix directly from the original UTF-8.
        var flags = options.Flags;
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
                var flag = pattern[end] switch { (byte)'i' => 2, (byte)'m' => 8, (byte)'s' => 16, (byte)'x' => 64, (byte)'a' => 256, (byte)'u' => 32, _ => 0 };
                if (flag == 0) break;
                if (flag == 256) nextFlags &= ~32;
                if (flag == 32) nextFlags &= ~256;
                if (flag == 64) nextVerbose = true;
                nextFlags |= flag; end++;
            }
            if (end == offset + 2 || end == pattern.Length || pattern[end] != (byte)')') break;
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
        public int Flags { get; }
        public int CaptureSlotCount { get; }
        private IReadOnlyList<LythonRegexNamedGroup>? _namedGroups;
        public IReadOnlyList<LythonRegexNamedGroup> NamedGroups => _namedGroups ?? throw new ObjectDisposedException(nameof(Pattern));

        internal Pattern(Utf8PythonRegex regex, int flags, int bytes, ILythonMemoryScope owner, CancellationToken token)
        {
            _regex = regex; Flags = flags; PatternBytes = bytes; _owner = owner;
            var names = regex.GetGroupNames();
            CaptureSlotCount = names.Length;
            var named = new List<LythonRegexNamedGroup>();
            for (var i = 0; i < names.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!int.TryParse(names[i], out _)) named.Add(new(Encoding.UTF8.GetBytes(names[i]), i));
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
            // PythonRe 0.4 owns only managed objects, with no program Dispose API.
            // Drop those references and retire their existing funded scope.
            if (Interlocked.Exchange(ref _regex, null) is null) return;
            _namedGroups = null;
            _owner.Dispose();
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
                    "PythonRe 0.4 does not support Python match with pos greater than endpos.");
            }
            Fund(MatchBytes);
            var data = Backend(() => mode switch
            {
                LythonRegexMatchMode.Match => Pattern.Regex.MatchDetailedData(BoundedInput.Span, _startByte, cancellationToken),
                LythonRegexMatchMode.FullMatch => Pattern.Regex.FullMatchDetailedData(BoundedInput.Span, _startByte, cancellationToken),
                _ => Pattern.Regex.SearchDetailedData(BoundedInput.Span, _startByte, cancellationToken),
            }, cancellationToken, patternSource: Pattern.Regex);
            if (!data.Success) return false;
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

        private static void CopyCaptures(Utf8PythonDetailedMatchData data, Span<LythonRegexCapture?> captures, CancellationToken token)
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

        public void Dispose() { if (!_disposed) { _disposed = true; _owner.Dispose(); } }

        private sealed class Cursor : ILythonRegexCursor
        {
            private readonly Subject _subject;
            private readonly ILythonMemoryScope _owner;
            private readonly CancellationTokenSource _cancellation;
            private Utf8PythonMatchCursor? _cursor;
            private bool _disposed;

            // Native exclusive scratch contributes its live high water, not
            // allocation traffic. Keep that conservative capacity until Close;
            // native Release must not call a possibly throwing host on cleanup.
            private sealed class WorkingAllowance : IUtf8MemoryAllowance
            {
                private readonly object _gate = new();
                private readonly ILythonMemoryReservation _storage;
                private long _liveBytes;

                internal WorkingAllowance(ILythonMemoryBudget owner) => _storage = Reserve(owner, 0);

                public bool TryReserve(ulong bytes)
                {
                    lock (_gate)
                    {
                        if (bytes > (ulong)(long.MaxValue - _liveBytes))
                            throw new LythonRegexException(LythonRegexFailure.MemoryAdmission,
                                "Shared memory budget refused regex storage.", requestedBytes: long.MaxValue);
                        var next = _liveBytes + (long)bytes;
                        // Throw at the first refused native admission. Returning
                        // false could make optional producer storage recoverable.
                        if (next > _storage.Bytes) Resize(_storage, next);
                        _liveBytes = next;
                        return true;
                    }
                }

                public void Release(ulong bytes)
                {
                    lock (_gate) _liveBytes -= (long)bytes;
                }
            }

            internal Cursor(Subject subject, CancellationToken token)
            {
                _subject = subject;
                _owner = subject._owner.CreateScope();
                _cancellation = new CancellationTokenSource();
                try
                {
                    if (subject._window.Pos > subject._window.EndPos) return;
                    // Native range publication refunds its staging; cover that
                    // array plus Lython capture conversion before either exists.
                    Reserve(_owner, checked(512L + 128L * subject.Pattern.CaptureSlotCount));
                    var working = new WorkingAllowance(_owner);
                    using var registration = Forward(token);
                    _cursor = Backend(() => subject.Pattern.Regex.CreateMatchCursor(subject.BoundedInput, subject._startByte, _cancellation.Token, working), token, patternSource: subject.Pattern.Regex);
                }
                catch { Dispose(); throw; }
            }

            private CancellationTokenRegistration Forward(CancellationToken token)
                => token.Register(static state => ((CancellationTokenSource)state!).Cancel(), _cancellation);

            public bool TryNext(Span<LythonRegexCapture?> captures, out int? lastIndex, CancellationToken token)
            {
                lastIndex = null;
                if (captures.Length != _subject.Pattern.CaptureSlotCount) throw new ArgumentException("Capture buffer length must equal CaptureSlotCount.", nameof(captures));
                token.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_cursor is null) return false;
                try
                {
                    using (Forward(token))
                    {
                        var next = Backend(() =>
                        {
                            var success = _cursor.MoveNext(out var match);
                            return (success, match);
                        }, token, patternSource: _subject.Pattern.Regex);
                        if (next.success)
                        {
                            CopyCaptures(next.match, captures, token);
                            lastIndex = next.match.LastIndex;
                            return true;
                        }
                    }
                    Close(); return false;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                { Close(); throw new OperationCanceledException(token); }
                catch { Close(); throw; }
            }

            private static void CopyCaptures(Utf8PythonRangeMatch data, Span<LythonRegexCapture?> captures, CancellationToken token)
            {
                for (var i = 0; i < captures.Length; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (!data.TryGetGroup(i, out var group) || !group.Success) { captures[i] = null; continue; }
                    captures[i] = new(group.StartOffsetInBytes, group.EndOffsetInBytes);
                }
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
