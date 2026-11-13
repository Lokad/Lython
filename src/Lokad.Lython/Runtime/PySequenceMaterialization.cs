namespace Lokad.Lython.Runtime;

internal static class PySequenceMaterialization
{
    // Caller admits final storage first. Copy in bounded chunks, doubling an
    // initialized prefix instead of making one managed call per repetition.
    internal static void FillRepeated<T>(ReadOnlySpan<T> source, Span<T> destination,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        if (destination.IsEmpty) return;
        if (source.IsEmpty || destination.Length % source.Length != 0)
            throw new ArgumentException("Repeated storage must contain complete source values.");
        var filled = 0;
        while (filled < destination.Length)
        {
            var count = filled == 0 ? source.Length : Math.Min(filled, destination.Length - filled);
            var input = filled == 0 ? source : destination[..count];
            for (var copied = 0; copied < count;)
            {
                context?.CheckExecution(span);
                var chunk = Math.Min(4096, count - copied);
                input.Slice(copied, chunk).CopyTo(destination.Slice(filled + copied, chunk));
                copied += chunk;
            }
            filled += count;
        }
        context?.CheckExecution(span);
    }

    public static object[] MaterializeSlice(
        IReadOnlyList<object> source,
        IEnumerable<int> indices,
        MemoryGovernor? memoryGovernor,
        LythonSourceSpan? allocationSpan)
    {
        if (indices is ICollection<int> collection)
        {
            using var storage = memoryGovernor?.ReserveTemporary(PyTuple.EstimateApproximateBytes(collection.Count), allocationSpan);
            var result = new object[collection.Count];
            var resultIndex = 0;
            foreach (var sourceIndex in indices)
            {
                result[resultIndex++] = source[sourceIndex];
            }

            storage?.Commit(PyTuple.EstimateApproximateBytes(result.Length));
            return result;
        }

        // Indices arrive without a count (for example lazily from slice bounds),
        // but each one addresses the already-sized source, so the result can
        // never exceed it. Reserve growth incrementally instead of pre-charging
        // the whole source, and let the owning container charge the final copy.
        using var temporary = memoryGovernor?.ReserveTemporary(0, allocationSpan);
        var values = new List<object>();
        long fundedCapacity = 0;
        foreach (var sourceIndex in indices)
        {
            if (temporary is not null && values.Count == values.Capacity)
            {
                // Charge the imminent backing-array growth before appending,
                // tracking funded (not observed) capacity like the shared
                // drains so each increment charges exactly once.
                var predicted = values.Capacity == 0 ? 4L : (long)values.Capacity * 2L;
                if (predicted > fundedCapacity)
                {
                    temporary.Grow(checked(16L * (predicted - fundedCapacity)), allocationSpan);
                    fundedCapacity = predicted;
                }
            }

            values.Add(source[sourceIndex]);
            if (values.Capacity > fundedCapacity)
            {
                temporary?.Grow(16L * (values.Capacity - fundedCapacity), allocationSpan);
                fundedCapacity = values.Capacity;
            }
        }

        var bytes = PyTuple.EstimateApproximateBytes(values.Count);
        temporary?.Grow(bytes, allocationSpan);
        var resultArray = values.ToArray();
        temporary?.Commit(bytes);
        return resultArray;
    }
}
