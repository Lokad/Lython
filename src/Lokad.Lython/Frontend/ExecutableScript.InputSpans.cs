namespace Lokad.Lython.Frontend;

internal readonly record struct ExecutableOperandSpan(long Key, LythonSourceSpan Span);

internal sealed partial class ExecutableCodeObject
{
    private readonly ExecutableOperandSpan[] _operandReadSpans;

    // Only an unbound direct local needs this metadata. Hot reads retain the
    // existing integer index; they do not fetch a second plan or span object.
    internal LythonSourceSpan OperandReadSpan(int instruction, int input)
    {
        var key = ((long)instruction << 2) | (uint)input;
        var low = 0;
        var high = _operandReadSpans.Length - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var item = _operandReadSpans[middle];
            if (item.Key == key) return item.Span;
            if (item.Key < key) low = middle + 1;
            else high = middle - 1;
        }
        return Instructions[instruction].Span;
    }
}
