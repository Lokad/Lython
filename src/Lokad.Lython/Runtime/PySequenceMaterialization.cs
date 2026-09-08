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

        // Indices arrive without a count (for example lazily from slice bounds),
        // but each one addresses the already-sized source, so the result can
        // never exceed it. Reserve growth incrementally instead of pre-charging
        // the whole source, and let the owning container charge the final copy.
        using var temporary = memoryGovernor?.ReserveTemporary(0, allocationSpan);
        var values = new List<object>();
        var chargedCapacity = 0;
        foreach (var sourceIndex in indices)
        {
            values.Add(source[sourceIndex]);
            if (values.Capacity > chargedCapacity)
            {
                temporary?.Grow(16L * (values.Capacity - chargedCapacity), allocationSpan);
                chargedCapacity = values.Capacity;
            }
        }

        temporary?.Grow(16L * values.Count, allocationSpan);
        return [.. values];
    }
}
