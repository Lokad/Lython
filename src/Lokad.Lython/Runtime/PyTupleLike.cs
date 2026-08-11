namespace Lokad.Lython.Runtime;

internal static class PyTupleLike
{
    public static int ComputeHashCode(IEnumerable<object> items)
    {
        var hash = new HashCode();
        foreach (var item in items)
        {
            hash.Add(PyValueComparer.Instance.GetHashCode(item));
        }

        return hash.ToHashCode();
    }

    public static PyTuple CreateSlice(IReadOnlyList<object> items, IEnumerable<int> indices)
        => new(indices.Select(index => items[index]));

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
