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
        var state = new WhitespaceRangeState(-1, mapping);
        long bytes = 128; // cursor metadata, including its retained source/state
        while (TryReadWhitespaceRange(value.Utf8Span, ref state, context, span, out var range))
            bytes = checked(bytes + PyString.EstimateApproximateBytes(range.Capacity));
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var result = new CasedWhitespaceIteration(value, upper, context, span);
        storage.Commit(bytes);
        return result;
    }

    internal sealed class CasedWhitespaceIteration
    {
        private readonly PyString _source;
        private readonly CaseMapping _mapping;
        private readonly LythonRuntime.ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private WhitespaceRangeState _state;

        internal CasedWhitespaceIteration(PyString source, bool upper,
            LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            _source = source;
            _mapping = upper ? CaseMapping.Upper : CaseMapping.Lower;
            _context = context;
            _span = span;
            _state = new WhitespaceRangeState(-1, _mapping);
        }

        internal PyIterationResult MoveNext()
        {
            // ForNext already checks the active execution before entering this
            // private cursor. The shared scanner/writer check inside long work;
            // publication retains the ordinary binding/next-opcode check.
            if (!TryReadWhitespaceRange(_source.Utf8Span, ref _state, _context, _span, out var range))
                return PyIterationResult.End;
            var word = CreateSplitFragment(_source.Utf8Span, range, _mapping,
                _context.MemoryGovernor, _span, _context);
            return PyIterationResult.Yield(word);
        }

        internal MemoryGovernor MemoryGovernor => _context.MemoryGovernor;

        // Only the closed native histogram segment borrows this value. Other
        // consumers keep MoveNext and receive their ordinary immutable strings.
        internal bool TryReadWord(out CompactWord word)
        {
            word = default;
            if (!TryReadWhitespaceRange(_source.Utf8Span, ref _state, _context, _span, out var range))
                return false;
            if (range.Capacity > PyString.InlineUtf8Capacity)
                word.Value = CreateSplitFragment(_source.Utf8Span, range, _mapping,
                    _context.MemoryGovernor, _span, _context);
            else
            {
                word.Capacity = range.Capacity;
                word.Length = WriteCaseUtf8(_source.Utf8Span[range.Start..range.End],
                    (Span<byte>)word.Utf8, _mapping, _context, _span);
            }
            return true;
        }

        // The eager preflight already funds this entire value. Publication is
        // also native unwind work: no admission, callback or execution check may
        // replace the pending failure while restoring the last Python binding.
        internal object Materialize(ref CompactWord word)
        {
            if (word.Value is not null) return word.Value;
            var value = PyString.CreateReservedUtf8(word.Capacity, _context.MemoryGovernor);
            CompactWordBytes(ref word).CopyTo(value.UnpublishedUtf8);
            value.CompleteUnpublishedUtf8(word.Length);
            word.Value = value;
            return value;
        }
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
        // Fixed, memory-safe stack scratch; larger results admit their backing
        // through the existing reservation before allocating a replacement.
        Span<(int Start, int End, int Capacity)> ranges = stackalloc (int, int, int)[8];
        var count = 0;
        long outputBytes = 0, scratchBytes = 0;
        MemoryGovernor.TemporaryMemoryReservation? scratch = null;
        try
        {
            if (separator is not null)
            {
                if (separator.ByteLength == 0) throw new InvalidOperationException("empty separator");
                CollectSeparatedRanges(source, separator.Utf8Span, maxSplit, reverse,
                    ref ranges, ref count, ref outputBytes, ref scratchBytes, ref scratch, governor, span, context);
            }
            else if (reverse)
                CollectWhitespaceReverse(source, maxSplit, ref ranges, ref count, ref outputBytes,
                    ref scratchBytes, ref scratch, governor, span, context);
            else
                CollectWhitespaceForward(source, maxSplit, ref ranges, ref count, ref outputBytes,
                    ref scratchBytes, ref scratch, governor, span, context, casing);

            // Admit final backing and all fragments together. Each value keeps
            // its own payload; adoption transfers the completed list capacity.
            // A whole unchanged source remains a funded alias in its owner.
            // Independent output owners still receive their own copied storage.
            var reuseWhole = casing is null && count == 1 && ranges[0].Start == 0 &&
                ranges[0].End == source.Length && ReferenceEquals(governor, value.OwnerMemoryGovernor);
            if (reuseWhole && ranges[0].Capacity != 0)
                outputBytes -= PyString.EstimateApproximateBytes(ranges[0].Capacity);
            var listBytes = PyList.EstimateCapacityBytes(count);
            var resultBytes = checked(outputBytes + listBytes);
            using var output = governor?.ReserveTemporary(resultBytes, span);
            context?.CheckExecution(span);
            var parts = count == 0 ? Array.Empty<object>() : new object[count];
            for (var index = 0; index < count; index++)
            {
                context?.CheckExecution(span);
                var range = ranges[reverse ? count - index - 1 : index];
                if (reuseWhole) { parts[index] = value; continue; }
                parts[index] = CreateSplitFragment(source, range, casing, governor, span, context);
            }
            var result = PyList.FromTransferredArray(parts, governor, span);
            context?.CheckExecution(span);
            output?.Commit(resultBytes);
            return result;
        }
        finally { scratch?.Dispose(); }
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
        ref int count, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor, LythonSourceSpan? span,
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
                reverse ? offset : found, ref outputBytes, ref scratchBytes, ref scratch, governor, span, context);
            offset = reverse ? found : found + separator.Length;
            splits++;
        }
        AppendSplitRange(ref ranges, ref count, reverse ? 0 : offset, reverse ? offset : source.Length,
            ref outputBytes, ref scratchBytes, ref scratch, governor, span, context);
    }

    private struct WhitespaceRangeState(int maxSplit, CaseMapping? casing)
    {
        internal int Position;
        internal int NextCheck;
        internal int Remaining = maxSplit < 0 ? int.MaxValue : maxSplit;
        internal readonly int MaximumCaseWidth = casing is null ? 0
            : casing == CaseMapping.Lower ? 4 : Math.Max(4, MaxCaseExpansionUtf8Bytes);
    }

    private static void CollectWhitespaceForward(ReadOnlySpan<byte> source, int maxSplit,
        scoped ref Span<(int Start, int End, int Capacity)> ranges, ref int count, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context, CaseMapping? casing)
    {
        var state = new WhitespaceRangeState(maxSplit, casing);
        while (TryReadWhitespaceRange(source, ref state, context, span, out var range))
            AppendSplitRange(ref ranges, ref count, range.Start, range.End, ref outputBytes,
                ref scratchBytes, ref scratch, governor, span, context, range.Capacity);
    }

    // One forward scanner serves sizing/materialization and native consumption.
    // State carries only positions and case bounds, never a borrowed byte span.
    private static bool TryReadWhitespaceRange(ReadOnlySpan<byte> source, ref WhitespaceRangeState state,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span,
        out (int Start, int End, int Capacity) range)
    {
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
            bool whitespace;
            if (source[position] < 0x80)
            {
                whitespace = IsAsciiWhitespace(source[position]);
                width = AsciiWhitespaceRunLength(source[position..state.NextCheck], whitespace);
            }
            else
            {
                Rune.DecodeFromUtf8(source[position..], out var rune, out width);
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
                    range = (start, position, state.MaximumCaseWidth == 0 ? position - start
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
            state.Position += width;
        }
        if (start >= 0)
        {
            range = (start, source.Length, state.MaximumCaseWidth == 0 ? source.Length - start
                : CheckCaseOutputCapacity(source.Length - start + extraCapacity, span));
            return true;
        }
        range = default;
        return false;
    }

    private static void CollectWhitespaceReverse(ReadOnlySpan<byte> source, int remaining,
        scoped ref Span<(int Start, int End, int Capacity)> ranges, ref int count, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor,
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
                            ref scratchBytes, ref scratch, governor, span, context);
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
                            ref scratchBytes, ref scratch, governor, span, context);
                        return;
                    }
                }
                position -= width;
            } while (position > chunkStart);
        }
        if (end >= 0)
            AppendSplitRange(ref ranges, ref count, 0, end, ref outputBytes,
                ref scratchBytes, ref scratch, governor, span, context);
    }

    private static void AppendSplitRange(scoped ref Span<(int Start, int End, int Capacity)> ranges,
        ref int count, int start, int end, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context, int? casedCapacity = null)
    {
        if (count == ranges.Length)
        {
            var capacity = checked(ranges.Length * 2);
            var bytes = checked(32L + 16L * capacity);
            if (governor is not null)
            {
                scratch ??= governor.ReserveTemporary(0, span);
                scratch.Grow(bytes, span);
            }
            try
            {
                context?.CheckExecution(span);
                var replacement = new (int Start, int End, int Capacity)[capacity];
                ranges[..count].CopyTo(replacement);
                ranges = replacement;
            }
            catch { scratch?.Shrink(bytes); throw; }
            scratch?.Shrink(scratchBytes);
            scratchBytes = bytes;
        }
        var outputCapacity = casedCapacity ?? end - start;
        ranges[count++] = (start, end, outputCapacity);
        if (outputCapacity != 0) outputBytes = checked(outputBytes + PyString.EstimateApproximateBytes(outputCapacity));
    }
}
