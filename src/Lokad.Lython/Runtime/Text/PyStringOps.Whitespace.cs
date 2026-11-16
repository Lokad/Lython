using System.Buffers;
using System.Text;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    // Python 3.13's 29 whitespace scalars. Keeping this small inventory explicit
    // avoids BCL Unicode-version drift and includes Python's four C0 separators.
    internal static bool IsPythonWhitespace(Rune rune) => rune.Value <= 0x7F
        ? IsAsciiWhitespace((byte)rune.Value)
        : rune.Value is 0x85 or 0xA0 or 0x1680 or >= 0x2000 and <= 0x200A or
            0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

    private static bool IsAsciiWhitespace(byte value)
        => value is >= 0x09 and <= 0x0D or >= 0x1C and <= 0x20;

    private static readonly SearchValues<byte> AsciiWhitespaceCharacters = CreateAsciiCharacters(whitespace: true);
    private static readonly SearchValues<byte> AsciiWordCharacters = CreateAsciiCharacters(whitespace: false);
    private static readonly SearchValues<byte> LineBreakCandidates = SearchValues.Create(
        (ReadOnlySpan<byte>)[(byte)'\n', (byte)'\r', (byte)'\v', (byte)'\f', 0x1c, 0x1d, 0x1e, 0xc2, 0xe2]);

    private static SearchValues<byte> CreateAsciiCharacters(bool whitespace)
    {
        Span<byte> characters = stackalloc byte[128];
        var count = 0;
        for (var value = 0; value < 128; value++)
            if (IsAsciiWhitespace((byte)value) == whitespace) characters[count++] = (byte)value;
        return SearchValues.Create(characters[..count]);
    }

    // The caller has classified the first (or last) ASCII byte. Stop at a
    // category change or any UTF-8 byte, leaving Unicode decoding to the caller.
    private static int AsciiWhitespaceRunLength(ReadOnlySpan<byte> chunk, bool whitespace, bool reverse = false)
    {
        var characters = whitespace ? AsciiWhitespaceCharacters : AsciiWordCharacters;
        if (reverse) return chunk.Length - 1 - chunk[..^1].LastIndexOfAnyExcept(characters);
        var boundary = chunk[1..].IndexOfAnyExcept(characters);
        return boundary < 0 ? chunk.Length : boundary + 1;
    }

    internal static PyList SplitCasedWhitespace(PyString value, bool upper,
        MemoryGovernor governor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => SplitCore(value, -1, false, governor, span, context,
            upper ? CaseMapping.Upper : CaseMapping.Lower);

    // Only compiler-private immediate iteration may consume this representation.
    // Admit every eventual word before the first loop effect, just as eager split
    // does. The execution owner retains unused capacity until its normal cleanup.
    internal static CasedWhitespaceIteration PrepareCasedWhitespaceIteration(PyString value, bool upper,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        var mapping = upper ? CaseMapping.Upper : CaseMapping.Lower;
        var prepared = context.MemoryGovernor.RentWordRanges(8, span);
        var previousScratchCapacity = prepared.EncodedCapacity;
        Span<(int Start, int End, int Capacity)> ranges = prepared.Ranges;
        var count = 0;
        long outputBytes = 0;
        try
        {
            CollectWhitespaceForward(value.Utf8Span, -1, ref ranges, ref count, ref outputBytes,
                ref prepared, context.MemoryGovernor, span, context, mapping, prepared);
            // The loan already owns the cursor and range capacity. Only eventual
            // immutable words need admission now; no transaction object per line.
            context.MemoryGovernor.Reserve(outputBytes, span);
            try
            {
                context.CheckExecution(span);
                var result = prepared!;
                result.Initialize(context, span, count);
                context.MemoryGovernor.Commit(outputBytes);
                prepared = null;
                return result;
            }
            catch { context.MemoryGovernor.ReleaseReserved(outputBytes); throw; }
        }
        finally
        {
            if (prepared is not null)
            {
                // Keep earlier reusable capacity, but never retain newly grown
                // working bytes from a failed scan. No cursor escaped.
                try
                {
                    if (prepared.EncodedCapacity > previousScratchCapacity)
                        prepared.ReleaseEncoded();
                }
                finally { context.MemoryGovernor.ReturnWordRanges(prepared, count); }
            }
        }
    }

    internal sealed class CasedWhitespaceIteration : ExecutableTemporaryValue
    {
        private readonly MemoryGovernor _owner;
        internal readonly (int Start, int End, int Capacity)[] Ranges;
        private byte[] _encoded = [];
        internal int EncodedCapacity => _encoded.Length;
        private LythonRuntime.ExecutionContext? _context;
        private LythonSourceSpan? _span;
        private int _count;
        private int _position;
        private bool _loaned = true;

        internal static long EstimateStorageBytes(int capacity) => checked(128L + 16L * capacity);
        internal long StorageBytes => checked(EstimateStorageBytes(Ranges.Length) + EncodedStorageBytes(_encoded.Length));

        internal CasedWhitespaceIteration(MemoryGovernor owner, int capacity)
        {
            _owner = owner;
            Ranges = new (int Start, int End, int Capacity)[capacity];
        }

        internal void AdoptScratchFrom(CasedWhitespaceIteration previous)
            => (_encoded, previous._encoded) = (previous._encoded, _encoded);

        internal void ReleaseStorage()
        {
            try { ReleaseEncoded(); }
            finally { _owner.Release(EstimateStorageBytes(Ranges.Length)); }
        }

        private static long EncodedStorageBytes(int capacity) => capacity == 0 ? 0 : 32L + capacity;

        internal Span<byte> PrepareEncoded(int capacity, LythonRuntime.ExecutionContext context, LythonSourceSpan? span)
        {
            if (capacity > _encoded.Length)
            {
                // Only a new line can replace scratch. Prior cached bytes have
                // no user; reserve overlap, but never copy them into the new line.
                var bytes = EncodedStorageBytes(capacity);
                using var replacement = _owner.ReserveTemporary(bytes, span);
                context.CheckExecution(span);
                var encoded = new byte[capacity];
                replacement.Commit(bytes);
                var previous = _encoded;
                _encoded = encoded;
                _owner.Release(EncodedStorageBytes(previous.Length));
            }
            return _encoded.AsSpan(0, capacity);
        }

        internal void ReleaseEncoded()
        {
            var previous = _encoded;
            _encoded = [];
            _owner.Release(EncodedStorageBytes(previous.Length));
        }

        internal void BeginLoan() => _loaned = true;

        internal bool EndLoan(int usedLength)
        {
            if (!_loaned) return false;
            _loaned = false;
            _context = null;
            _span = null;
            _count = _position = 0;
            Array.Clear(Ranges, 0, usedLength);
            return true;
        }

        internal void Initialize(LythonRuntime.ExecutionContext context, LythonSourceSpan span, int count)
        {
            _context = context;
            _span = span;
            _count = count;
            _position = 0;
        }

        internal PyIterationResult MoveNext()
        {
            // ForNext already checks the active execution before entering this
            // private cursor. The shared writer checks inside long work;
            // publication retains the ordinary binding/next-opcode check.
            if (_position == _count) return PyIterationResult.End;
            var range = Ranges[_position++];
            var word = CreateSplitFragment(_encoded.AsSpan(), range, null,
                _owner, _span, _context);
            return PyIterationResult.Yield(word);
        }

        internal MemoryGovernor MemoryGovernor => _owner;

        // Only the closed native histogram segment borrows this value. Other
        // consumers keep MoveNext and receive their ordinary immutable strings.
        internal bool TryReadWord(out CompactWord word)
        {
            word = default;
            if (_position == _count) return false;
            var range = Ranges[_position++];
            if (range.Capacity > PyString.InlineUtf8Capacity)
                word.Value = CreateSplitFragment(_encoded.AsSpan(), range, null,
                    _owner, _span, _context);
            else
            {
                word.Capacity = range.Capacity;
                word.Length = range.End - range.Start;
                _encoded.AsSpan()[range.Start..range.End].CopyTo((Span<byte>)word.Utf8);
            }
            return true;
        }

        // The eager preflight already funds this entire value. Publication is
        // also native unwind work: no admission, callback or execution check may
        // replace the pending failure while restoring the last Python binding.
        internal object Materialize(ref CompactWord word)
        {
            if (word.Value is not null) return word.Value;
            var value = PyString.CreateReservedUtf8(word.Capacity, _owner);
            CompactWordBytes(ref word).CopyTo(value.UnpublishedUtf8);
            value.CompleteUnpublishedUtf8(word.Length);
            word.Value = value;
            return value;
        }

        public override void Dispose()
            => _owner.ReturnWordRanges(this, _count);
    }

    [System.Runtime.CompilerServices.InlineArray(PyString.InlineUtf8Capacity)]
    internal struct CompactWordUtf8 { private byte _first; }

    internal struct CompactWord
    {
        internal object? Value;
        internal CompactWordUtf8 Utf8;
        internal int Length, Capacity;
    }

    internal static ReadOnlySpan<byte> CompactWordBytes(ref CompactWord word)
        => ((ReadOnlySpan<byte>)word.Utf8)[..word.Length];

    private static PyList SplitCore(PyString value, int maxSplit, bool reverse,
        MemoryGovernor? governor, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context,
        CaseMapping? casing = null, PyString? separator = null)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= PyStructuralGuard.AmbientSpan;
        context ??= PyStructuralGuard.AmbientContext;
        governor?.ThrowIfAdmissionFailed();
        context?.CheckExecution(span);
        var source = value.Utf8Span;
        // Small splits use fixed stack storage. Larger inventories grow directly
        // in admitted execution-owned buffers shared with private iteration.
        Span<(int Start, int End, int Capacity)> ranges = stackalloc (int, int, int)[8];
        var count = 0;
        long outputBytes = 0;
        CasedWhitespaceIteration? prepared = null;
        try
        {
            if (separator is not null)
            {
                if (separator.ByteLength == 0) throw new InvalidOperationException("empty separator");
                CollectSeparatedRanges(source, separator.Utf8Span, maxSplit, reverse,
                    ref ranges, ref count, ref outputBytes, ref prepared, governor, span, context);
            }
            else if (reverse)
                CollectWhitespaceReverse(source, maxSplit, ref ranges, ref count, ref outputBytes,
                    ref prepared, governor, span, context);
            else
                CollectWhitespaceForward(source, maxSplit, ref ranges, ref count, ref outputBytes,
                    ref prepared, governor, span, context, casing);

            return BuildSplitResult(value, ranges[..count], outputBytes, reverse, casing, governor, span, context);
        }
        finally { if (prepared is not null) governor?.ReturnWordRanges(prepared, count); }
    }

    private static PyList SplitLineRanges(PyString value, bool keepEnds, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= PyStructuralGuard.AmbientSpan;
        context ??= PyStructuralGuard.AmbientContext;
        governor?.ThrowIfAdmissionFailed();
        context?.CheckExecution(span);
        Span<(int Start, int End, int Capacity)> ranges = stackalloc (int, int, int)[8];
        var count = 0;
        long outputBytes = 0;
        CasedWhitespaceIteration? prepared = null;
        try
        {
            var source = value.Utf8Span;
            var start = 0;
            for (var offset = 0; offset < source.Length;)
            {
                context?.CheckExecution(span);
                var end = offset + Math.Min(4096, source.Length - offset);
                while (offset < end)
                {
                    var candidate = source[offset..end].IndexOfAny(LineBreakCandidates);
                    if (candidate < 0) { offset = end; break; }
                    offset += candidate;
                    if (TryGetLineBreakByteLength(source, offset, out var width))
                    {
                        var after = offset + width;
                        AppendSplitRange(ref ranges, ref count, start, keepEnds ? after : offset,
                            ref outputBytes, ref prepared, governor, span, context);
                        start = offset = after;
                    }
                    else offset++;
                }
            }
            if (start < source.Length)
                AppendSplitRange(ref ranges, ref count, start, source.Length,
                    ref outputBytes, ref prepared, governor, span, context);
            return BuildSplitResult(value, ranges[..count], outputBytes, false, null, governor, span, context);
        }
        finally { if (prepared is not null) governor?.ReturnWordRanges(prepared, count); }
    }

    private static PyList BuildSplitResult(PyString value, ReadOnlySpan<(int Start, int End, int Capacity)> ranges,
        long outputBytes, bool reverse, CaseMapping? casing, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        // All split forms admit final backing/fragments together, then adopt the
        // completed array. Only a whole unchanged same-owner source is reused;
        // other fragments keep their independent payload and owner.
        var source = value.Utf8Span;
        var reuseWhole = casing is null && ranges.Length == 1 && ranges[0].Start == 0 &&
            ranges[0].End == source.Length && ReferenceEquals(governor, value.OwnerMemoryGovernor);
        if (reuseWhole && ranges[0].Capacity != 0)
            outputBytes -= PyString.EstimateApproximateBytes(ranges[0].Capacity);
        var resultBytes = checked(outputBytes + PyList.EstimateCapacityBytes(ranges.Length));
        using var output = governor?.ReserveTemporary(resultBytes, span);
        context?.CheckExecution(span);
        var parts = ranges.IsEmpty ? Array.Empty<object>() : new object[ranges.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            context?.CheckExecution(span);
            var range = ranges[reverse ? parts.Length - index - 1 : index];
            parts[index] = reuseWhole ? value : CreateSplitFragment(source, range, casing, governor, span, context);
        }
        var result = PyList.FromTransferredArray(parts, governor, span);
        context?.CheckExecution(span);
        output?.Commit(resultBytes);
        return result;
    }

    private static PyString CreateSplitFragment(ReadOnlySpan<byte> source,
        (int Start, int End, int Capacity) range, CaseMapping? casing,
        MemoryGovernor? governor, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        if (range.Capacity == 0) return PyString.Empty;
        var fragment = PyString.CreateReservedUtf8(range.Capacity, governor);
        var bytes = fragment.UnpublishedUtf8;
        var written = bytes.Length;
        if (casing is { } mapping)
        {
            // Whitespace is case-stable and breaks final-sigma context.
            written = WriteCaseUtf8(source[range.Start..range.End], bytes, mapping, context, span);
        }
        else
            for (var copied = 0; copied < bytes.Length;)
            {
                context?.CheckExecution(span);
                var chunk = Math.Min(4096, bytes.Length - copied);
                source.Slice(range.Start + copied, chunk).CopyTo(bytes.Slice(copied, chunk));
                copied += chunk;
            }
        fragment.CompleteUnpublishedUtf8(written);
        return fragment;
    }


    private static void CollectSeparatedRanges(ReadOnlySpan<byte> source, ReadOnlySpan<byte> separator,
        int maxSplit, bool reverse, scoped ref Span<(int Start, int End, int Capacity)> ranges,
        ref int count, ref long outputBytes,
        ref CasedWhitespaceIteration? prepared, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context)
    {
        var offset = reverse ? source.Length : 0;
        var splits = 0;
        while (maxSplit < 0 || splits < maxSplit)
        {
            var remaining = reverse ? source[..offset] : source[offset..];
            var found = LythonRuntime.FindBytes(remaining, separator, context, span, reverse);
            if (found < 0) break;
            if (!reverse) found += offset;
            AppendSplitRange(ref ranges, ref count, reverse ? found + separator.Length : offset,
                reverse ? offset : found, ref outputBytes, ref prepared, governor, span, context);
            offset = reverse ? found : found + separator.Length;
            splits++;
        }
        AppendSplitRange(ref ranges, ref count, reverse ? 0 : offset, reverse ? offset : source.Length,
            ref outputBytes, ref prepared, governor, span, context);
    }

    private struct WhitespaceRangeState(int maxSplit, CaseMapping? casing)
    {
        internal int Position;
        internal int Written;
        internal int NextCheck;
        internal int Remaining = maxSplit < 0 ? int.MaxValue : maxSplit;
        internal readonly CaseMapping? Mapping = casing;
        internal readonly int MaximumCaseWidth = casing is null ? 0
            : casing == CaseMapping.Lower ? 4 : Math.Max(4, MaxCaseExpansionUtf8Bytes);
    }

    private static void CollectWhitespaceForward(ReadOnlySpan<byte> source, int maxSplit,
        scoped ref Span<(int Start, int End, int Capacity)> ranges, ref int count, ref long outputBytes,
        ref CasedWhitespaceIteration? prepared, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context, CaseMapping? casing,
        CasedWhitespaceIteration? encoded = null)
    {
        var state = new WhitespaceRangeState(maxSplit, casing);
        // The source byte count bounds its scalar count. Admit the entire
        // conservative case expansion before obtaining this one stable window;
        // no per-word resize/admission or extra sizing scan is needed.
        var encodedBytes = encoded is null ? default : encoded.PrepareEncoded(
            CheckCaseOutputCapacity((long)source.Length * state.MaximumCaseWidth, span), context!, span);
        context?.CheckExecution(span);
        while (TryReadWhitespaceRange(source, ref state, context, span, out var range, encodedBytes))
            AppendSplitRange(ref ranges, ref count, range.Start, range.End, ref outputBytes,
                ref prepared, governor, span, context, range.Capacity);
    }

    // One forward scanner serves sizing/materialization and native consumption.
    // State carries only positions and case bounds, never a borrowed byte span.
    private static bool TryReadWhitespaceRange(ReadOnlySpan<byte> source, ref WhitespaceRangeState state,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span,
        out (int Start, int End, int Capacity) range, Span<byte> encodedBytes = default)
    {
        var outputStart = state.Written;
        var writeCased = !encodedBytes.IsEmpty;
        var start = -1;
        long extraCapacity = 0;
        while (state.Position < source.Length)
        {
            if (state.Position >= state.NextCheck)
            {
                context?.CheckExecution(span);
                state.NextCheck = state.Position + Math.Min(64, source.Length - state.Position);
            }
            var position = state.Position;
            int width;
            Rune rune = default;
            bool whitespace;
            if (source[position] < 0x80)
            {
                whitespace = IsAsciiWhitespace(source[position]);
                width = AsciiWhitespaceRunLength(source[position..state.NextCheck], whitespace);
            }
            else
            {
                Rune.DecodeFromUtf8(source[position..], out rune, out width);
                whitespace = IsPythonWhitespace(rune);
                if (!whitespace && state.MaximumCaseWidth != 0)
                    extraCapacity += state.MaximumCaseWidth - width;
            }
            if (whitespace)
            {
                if (start >= 0)
                {
                    state.Position += width;
                    state.Remaining--;
                    range = writeCased ? (outputStart, state.Written, state.Written - outputStart)
                        : (start, position, state.MaximumCaseWidth == 0 ? position - start
                            : CheckCaseOutputCapacity(position - start + extraCapacity, span));
                    return true;
                }
            }
            else if (start < 0)
            {
                start = position;
                if (state.Remaining == 0)
                {
                    state.Position = source.Length;
                    range = (start, source.Length, source.Length - start);
                    return true;
                }
            }
            if (!whitespace && writeCased)
            {
                var mapping = state.Mapping!.Value;
                if (source[position] < 0x80)
                {
                    var output = encodedBytes.Slice(state.Written, width);
                    var previousWasCased = false;
                    WriteAsciiCase(source.Slice(position, width), output, mapping, position, ref previousWasCased);
                    state.Written += width;
                }
                else
                {
                    var output = encodedBytes.Slice(state.Written, state.MaximumCaseWidth);
                    state.Written += WriteCaseRune(rune, mapping, source, position, output, context, span);
                }
            }
            state.Position += width;
        }
        if (start >= 0)
        {
            range = writeCased ? (outputStart, state.Written, state.Written - outputStart)
                : (start, source.Length, state.MaximumCaseWidth == 0 ? source.Length - start
                    : CheckCaseOutputCapacity(source.Length - start + extraCapacity, span));
            return true;
        }
        range = default;
        return false;
    }

    private static void CollectWhitespaceReverse(ReadOnlySpan<byte> source, int remaining,
        scoped ref Span<(int Start, int End, int Capacity)> ranges, ref int count, ref long outputBytes,
        ref CasedWhitespaceIteration? prepared, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        var end = -1;
        for (var position = source.Length; position > 0;)
        {
            context?.CheckExecution(span);
            var chunkStart = Math.Max(0, position - 64);
            do
            {
                int width;
                bool whitespace;
                if (source[position - 1] < 0x80)
                {
                    whitespace = IsAsciiWhitespace(source[position - 1]);
                    width = AsciiWhitespaceRunLength(source[chunkStart..position], whitespace, reverse: true);
                }
                else
                {
                    Rune.DecodeLastFromUtf8(source[..position], out var rune, out width);
                    whitespace = IsPythonWhitespace(rune);
                }
                if (whitespace)
                {
                    if (end >= 0)
                    {
                        AppendSplitRange(ref ranges, ref count, position, end, ref outputBytes,
                            ref prepared, governor, span, context);
                        end = -1;
                        remaining--;
                    }
                }
                else if (end < 0)
                {
                    end = position;
                    if (remaining == 0)
                    {
                        AppendSplitRange(ref ranges, ref count, 0, end, ref outputBytes,
                            ref prepared, governor, span, context);
                        return;
                    }
                }
                position -= width;
            } while (position > chunkStart);
        }
        if (end >= 0)
            AppendSplitRange(ref ranges, ref count, 0, end, ref outputBytes,
                ref prepared, governor, span, context);
    }

    private static void AppendSplitRange(scoped ref Span<(int Start, int End, int Capacity)> ranges,
        ref int count, int start, int end, ref long outputBytes,
        ref CasedWhitespaceIteration? prepared, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context, int? casedCapacity = null)
    {
        if (count == ranges.Length)
        {
            var capacity = checked(ranges.Length * 2);
            context?.CheckExecution(span);
            // Keep the previous loan funded through admission and copying. The
            // execution cache owns returned capacity; active cursors own loans.
            var replacement = governor?.RentWordRanges(capacity, span);
            var values = replacement?.Ranges ?? new (int Start, int End, int Capacity)[capacity];
            var previous = prepared;
            ranges[..count].CopyTo(values);
            prepared = replacement;
            ranges = values;
            if (previous is not null)
            {
                // The scanner keeps this same byte builder while range capacity
                // grows; transfer it before the old cursor can reenter the cache.
                replacement?.AdoptScratchFrom(previous);
                governor?.ReturnWordRanges(previous, count);
            }
        }
        var outputCapacity = casedCapacity ?? end - start;
        ranges[count++] = (start, end, outputCapacity);
        if (outputCapacity != 0) outputBytes = checked(outputBytes + PyString.EstimateApproximateBytes(outputCapacity));
    }
}
