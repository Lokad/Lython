using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // One construction pass for immutable string/byte replacement. A missing
    // needle returns no storage; callers retain their already funded receiver.
    internal static GovernedByteBuilder? ReplaceByteContent(ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> oldValue, ReadOnlySpan<byte> newValue, int count, bool scalarBoundaries,
        MemoryGovernor? governor, ExecutionContext? context, LythonSourceSpan? span)
    {
        context?.CheckExecution(span);
        var remaining = count < 0 ? long.MaxValue : count;
        var firstMatch = oldValue.IsEmpty ? 0 : FindBytes(source, oldValue, context, span);
        if (firstMatch < 0) return null;
        var capacity = source.Length;
        if (oldValue.IsEmpty)
        {
            long insertions;
            if (!scalarBoundaries) insertions = Math.Min((long)source.Length + 1, remaining);
            else
            {
                insertions = 1;
                for (var offset = 0; offset < source.Length && insertions < remaining; insertions++)
                {
                    context?.CheckExecution(span);
                    offset += ReplacementScalarWidth(source[offset]);
                }
            }
            var length = checked(source.Length + insertions * newValue.Length);
            if (length > int.MaxValue)
                throw RuntimeErrors.Runtime(scalarBoundaries ? "string allocation is too large."
                    : "byte buffer allocation is too large.", span);
            if (length > Array.MaxLength)
                throw RuntimeErrors.Memory("replacement result exceeds supported capacity", span);
            capacity = (int)length;
        }
        context?.CheckExecution(span);
        var result = governor is null ? new GovernedByteBuilder(capacity)
            : new GovernedByteBuilder(governor, span, capacity);
        try
        {
            var offset = 0;
            if (oldValue.IsEmpty)
            {
                AppendReplacementPart(result, newValue, context, span);
                while (offset < source.Length && --remaining > 0)
                {
                    context?.CheckExecution(span);
                    var width = scalarBoundaries ? ReplacementScalarWidth(source[offset]) : 1;
                    result.Append(source.Slice(offset, width));
                    offset += width;
                    AppendReplacementPart(result, newValue, context, span);
                }
            }
            else
            {
                var found = firstMatch;
                while (found >= 0 && remaining-- > 0)
                {
                    context?.CheckExecution(span);
                    AppendReplacementPart(result, source.Slice(offset, found), context, span);
                    AppendReplacementPart(result, newValue, context, span);
                    offset += found + oldValue.Length;
                    if (remaining == 0) break;
                    found = FindBytes(source[offset..], oldValue, context, span);
                }
            }
            AppendReplacementPart(result, source[offset..], context, span);
            context?.CheckExecution(span);
            return result;
        }
        catch { result.Release(); throw; }
    }

    private static int ReplacementScalarWidth(byte lead)
        => lead < 0x80 ? 1 : lead < 0xe0 ? 2 : lead < 0xf0 ? 3 : 4;

    private static void AppendReplacementPart(GovernedByteBuilder result, ReadOnlySpan<byte> source,
        ExecutionContext? context, LythonSourceSpan? span)
    {
        while (source.Length > 16384)
        {
            context?.CheckExecution(span);
            result.Append(source[..16384]);
            source = source[16384..];
        }
        result.Append(source);
    }
}
