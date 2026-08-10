namespace Lokad.Lython.Runtime;

internal static class PyTupleLike
{
    public static bool TryGetItems(object value, out IReadOnlyList<object> items)
    {
        switch (value)
        {
            case PyTuple tuple:
                items = tuple;
                return true;
            case PyNamedTupleObject namedTuple:
                items = namedTuple;
                return true;
            case PyTypingNamedTupleObject typingNamedTuple:
                items = typingNamedTuple;
                return true;
            case LythonRuntime.TimeStructTimeValue structTime:
                items = structTime;
                return true;
            default:
                items = Array.Empty<object>();
                return false;
        }
    }
}
