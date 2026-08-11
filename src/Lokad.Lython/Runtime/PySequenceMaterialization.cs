namespace Lokad.Lython.Runtime;

internal static class PySequenceMaterialization
{
    public static object[] MaterializeSlice(
        IReadOnlyList<object> source,
        IEnumerable<int> indices,
        MemoryGovernor? memoryGovernor,
        LythonSourceSpan? allocationSpan)
    {
        if (indices is ICollection<int> collection)
        {
            memoryGovernor?.EnsureCanReserve(PyTuple.EstimateApproximateBytes(collection.Count), allocationSpan);
            var result = new object[collection.Count];
            var resultIndex = 0;
            foreach (var sourceIndex in indices)
            {
                result[resultIndex++] = source[sourceIndex];
            }

            return result;
        }

        var values = new List<object>();
        foreach (var sourceIndex in indices)
        {
            values.Add(source[sourceIndex]);
        }

        return [.. values];
    }
}
