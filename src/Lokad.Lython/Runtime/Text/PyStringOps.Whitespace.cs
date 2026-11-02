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

    private static PyList SplitWhitespaceCore(PyString value, int maxSplit, bool reverse,
        MemoryGovernor? governor, LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
    {
        governor ??= value.OwnerMemoryGovernor;
        span ??= value.AllocationSpan;
        context ??= PyStructuralGuard.AmbientContext;
        governor?.ThrowIfAdmissionFailed();
        context?.CheckExecution(span);
        var source = value.Utf8Bytes.Span;
        // Fixed, memory-safe stack scratch; larger results admit their backing
        // through the existing reservation before allocating a replacement.
        Span<(int Start, int End)> ranges = stackalloc (int, int)[8];
        var count = 0;
        long outputBytes = 0, scratchBytes = 0;
        MemoryGovernor.TemporaryMemoryReservation? scratch = null;
        try
        {
            if (reverse)
                CollectWhitespaceReverse(source, maxSplit, ref ranges, ref count, ref outputBytes,
                    ref scratchBytes, ref scratch, governor, span, context);
            else
                CollectWhitespaceForward(source, maxSplit, ref ranges, ref count, ref outputBytes,
                    ref scratchBytes, ref scratch, governor, span, context);

            // Admit all fragment capacity/metadata once. Commit transfers each
            // fragment's funding without another shared-allowance resize.
            using var output = governor?.ReserveTemporary(outputBytes, span);
            context?.CheckExecution(span);
            var parts = governor is null ? new PyList() : new PyList(count, governor, span);
            for (var index = 0; index < count; index++)
            {
                context?.CheckExecution(span);
                var range = ranges[reverse ? count - index - 1 : index];
                var bytes = new byte[range.End - range.Start];
                for (var copied = 0; copied < bytes.Length;)
                {
                    context?.CheckExecution(span);
                    var chunk = Math.Min(4096, bytes.Length - copied);
                    source.Slice(range.Start + copied, chunk).CopyTo(bytes.AsSpan(copied, chunk));
                    copied += chunk;
                }
                var fragment = governor is null ? PyString.FromOwnedUtf8(bytes)
                    : PyString.FromOwnedUtf8(bytes, governor, span, output!);
                parts.Add(fragment);
            }
            context?.CheckExecution(span);
            return parts;
        }
        finally { scratch?.Dispose(); }
    }

    private static void CollectWhitespaceForward(ReadOnlySpan<byte> source, int maxSplit,
        scoped ref Span<(int Start, int End)> ranges, ref int count, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
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
                        ref scratchBytes, ref scratch, governor, span, context);
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
                        ref scratchBytes, ref scratch, governor, span, context);
                    return;
                }
            }
            position += width;
        }
        if (start >= 0)
            AppendWhitespaceRange(ref ranges, ref count, start, source.Length, ref outputBytes,
                ref scratchBytes, ref scratch, governor, span, context);
    }

    private static void CollectWhitespaceReverse(ReadOnlySpan<byte> source, int remaining,
        scoped ref Span<(int Start, int End)> ranges, ref int count, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor,
        LythonSourceSpan? span, LythonRuntime.ExecutionContext? context)
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
                    AppendWhitespaceRange(ref ranges, ref count, 0, end, ref outputBytes,
                        ref scratchBytes, ref scratch, governor, span, context);
                    return;
                }
            }
            position -= width;
        }
        if (end >= 0)
            AppendWhitespaceRange(ref ranges, ref count, 0, end, ref outputBytes,
                ref scratchBytes, ref scratch, governor, span, context);
    }

    private static void AppendWhitespaceRange(scoped ref Span<(int Start, int End)> ranges,
        ref int count, int start, int end, ref long outputBytes, ref long scratchBytes,
        ref MemoryGovernor.TemporaryMemoryReservation? scratch, MemoryGovernor? governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context)
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
                var replacement = new (int Start, int End)[capacity];
                ranges[..count].CopyTo(replacement);
                ranges = replacement;
            }
            catch { scratch?.Shrink(bytes); throw; }
            scratch?.Shrink(scratchBytes);
            scratchBytes = bytes;
        }
        ranges[count++] = (start, end);
        outputBytes = checked(outputBytes + PyString.EstimateApproximateBytes(end - start));
    }
}
