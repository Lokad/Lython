using System.Text;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    // Python 3.13's 29 whitespace scalars. Keeping this small inventory explicit
    // avoids BCL Unicode-version drift and includes Python's four C0 separators.
    internal static bool IsPythonWhitespace(Rune rune) => rune.Value is
        >= 0x09 and <= 0x0D or >= 0x1C and <= 0x20 or
        0x85 or 0xA0 or 0x1680 or >= 0x2000 and <= 0x200A or
        0x2028 or 0x2029 or 0x202F or 0x205F or 0x3000;

    internal static PyList SplitCasedWhitespace(PyString value, bool upper,
        MemoryGovernor governor, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        => SplitWhitespaceCore(value, -1, false, governor, span, context,
            upper ? CaseMapping.Upper : CaseMapping.Lower);

    private static PyList SplitWhitespaceCore(PyString value, int maxSplit, bool reverse,
        MemoryGovernor? governor, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context,
        CaseMapping? casing = null)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        context ??= PyStructuralGuard.AmbientContext;
        governor?.ThrowIfAdmissionFailed();
        context?.CheckExecution(span);
        var source = value.Utf8Bytes.Span;
        // Fixed, memory-safe stack scratch; larger results admit their backing
        // through the existing reservation before allocating a replacement.
        Span<(int Start, int End, int Capacity)> ranges = stackalloc (int, int, int)[8];
        var count = 0;
        long outputBytes = 0, scratchBytes = 0;
        MemoryGovernor.TemporaryMemoryReservation? scratch = null;
        try
        {
            if (reverse)
                CollectWhitespaceReverse(source, maxSplit, ref ranges, ref count, ref outputBytes,
                    ref scratchBytes, ref scratch, governor, span, context, casing);
            else
                CollectWhitespaceForward(source, maxSplit, ref ranges, ref count, ref outputBytes,
                    ref scratchBytes, ref scratch, governor, span, context, casing);

            // Admit all fragment capacity/metadata once. Commit transfers each
            // fragment's funding without another shared-allowance resize.
            using var output = governor?.ReserveTemporary(outputBytes, span);
            context?.CheckExecution(span);
            var parts = governor is null ? new PyList() : new PyList(count, governor, span);
            for (var index = 0; index < count; index++)
            {
                context?.CheckExecution(span);
                var range = ranges[reverse ? count - index - 1 : index];
                var bytes = new byte[range.Capacity];
                var written = bytes.Length;
                if (casing is { } mapping)
                {
                    // Whitespace is case-stable and breaks final-sigma context.
                    // Write final words without materializing a cased source.
                    written = WriteCaseUtf8(source[range.Start..range.End], bytes, mapping, context, span);
                }
                else
                    for (var copied = 0; copied < bytes.Length;)
                    {
                        context?.CheckExecution(span);
                        var chunk = Math.Min(4096, bytes.Length - copied);
                        source.Slice(range.Start + copied, chunk).CopyTo(bytes.AsSpan(copied, chunk));
                        copied += chunk;
                    }
                var fragment = governor is null ? PyString.FromReservedUtf8(bytes, written, null, span)
                    : PyString.FromOwnedUtf8(bytes, governor, span, output!, written);
                parts.Add(fragment);
            }
            context?.CheckExecution(span);
            return parts;
        }
        finally { scratch?.Dispose(); }
    }

    private static void CollectWhitespaceForward(ReadOnlySpan<byte> source, int maxSplit,
        scoped ref Span<(int Start, int End, int Capacity)> ranges, ref int count, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context, CaseMapping? casing)
    {
        var start = -1;
        var remaining = maxSplit < 0 ? int.MaxValue : maxSplit;
        for (var position = 0; position < source.Length;)
        {
            PyStructuralGuard.NoteWork(context, span);
            Rune.DecodeFromUtf8(source[position..], out var rune, out var width);
            if (IsPythonWhitespace(rune))
            {
                if (start >= 0)
                {
                    AppendWhitespaceRange(ref ranges, ref count, start, position, ref outputBytes,
                        ref scratchBytes, ref scratch, governor, span, context, source, casing);
                    start = -1;
                    remaining--;
                }
            }
            else if (start < 0)
            {
                start = position;
                if (remaining == 0)
                {
                    AppendWhitespaceRange(ref ranges, ref count, start, source.Length, ref outputBytes,
                        ref scratchBytes, ref scratch, governor, span, context, source, casing);
                    return;
                }
            }
            position += width;
        }
        if (start >= 0)
            AppendWhitespaceRange(ref ranges, ref count, start, source.Length, ref outputBytes,
                ref scratchBytes, ref scratch, governor, span, context, source, casing);
    }

    private static void CollectWhitespaceReverse(ReadOnlySpan<byte> source, int remaining,
        scoped ref Span<(int Start, int End, int Capacity)> ranges, ref int count, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context, CaseMapping? casing)
    {
        var end = -1;
        for (var position = source.Length; position > 0;)
        {
            PyStructuralGuard.NoteWork(context, span);
            Rune.DecodeLastFromUtf8(source[..position], out var rune, out var width);
            if (IsPythonWhitespace(rune))
            {
                if (end >= 0)
                {
                    AppendWhitespaceRange(ref ranges, ref count, position, end, ref outputBytes,
                        ref scratchBytes, ref scratch, governor, span, context, source, casing);
                    end = -1;
                    remaining--;
                }
            }
            else if (end < 0)
            {
                end = position;
                if (remaining == 0)
                {
                    AppendWhitespaceRange(ref ranges, ref count, 0, end, ref outputBytes,
                        ref scratchBytes, ref scratch, governor, span, context, source, casing);
                    return;
                }
            }
            position -= width;
        }
        if (end >= 0)
            AppendWhitespaceRange(ref ranges, ref count, 0, end, ref outputBytes,
                ref scratchBytes, ref scratch, governor, span, context, source, casing);
    }

    private static void AppendWhitespaceRange(scoped ref Span<(int Start, int End, int Capacity)> ranges,
        ref int count, int start, int end, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context, ReadOnlySpan<byte> source, CaseMapping? casing)
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
        var outputCapacity = casing is { } mapping
            ? CaseOutputCapacity(source[start..end], mapping, context, span) : end - start;
        ranges[count++] = (start, end, outputCapacity);
        outputBytes = checked(outputBytes + PyString.EstimateApproximateBytes(outputCapacity));
    }
}
