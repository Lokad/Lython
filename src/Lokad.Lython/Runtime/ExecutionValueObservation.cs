using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class ExecutionValueObservation
{
    public ExecutionValueObservation(ExecutionState state)
    {
        State = state;
    }

    public ExecutionState State { get; }

    public LythonRuntime.ExecutionLimits Limits => State.Limits;

    public void ObserveString(PyString text, LythonSourceSpan? span)
    {
        EnforceStringLengthLimit(text, span);

    }

    public void ObserveCollectionCount(int count, LythonSourceSpan? span)
    {
        EnforceCollectionCountLimit(count, span);
    }

    public void ObserveValue(object value, LythonSourceSpan? span)
    {
        EnforceValueLimits(value, span);
    }

    private void EnforceStringLengthLimit(PyString text, LythonSourceSpan? span)
    {
        if (Limits.MaxStringLength is { } maxStringLength && text.Length > maxStringLength)
        {
            throw RuntimeErrors.Attributed("RuntimeError", $"maximum string length exceeded ({maxStringLength})", span,
                LythonFailureReason.StringLengthLimit, maxStringLength, text.Length);
        }
    }

    private void EnforceCollectionCountLimit(int count, LythonSourceSpan? span)
    {
        if (Limits.MaxCollectionSize is { } maxCollectionSize && count > maxCollectionSize)
        {
            throw RuntimeErrors.Attributed("RuntimeError", $"maximum collection size exceeded ({maxCollectionSize})", span,
                LythonFailureReason.CollectionSizeLimit, maxCollectionSize, count);
        }
    }

    private void EnforceValueLimits(object value, LythonSourceSpan? span)
    {
        if (value is PyString text)
        {
            EnforceStringLengthLimit(text, span);
        }

        if (TryGetCollectionCount(value) is { } count)
        {
            EnforceCollectionCountLimit(count, span);
        }
    }

    private static int? TryGetCollectionCount(object value) => value switch
    {
        PyList list => list.Count,
        PyTuple tuple => tuple.Count,
        PyDict dict => dict.Count,
        PySet set => set.Count,
        _ => null
    };
}
