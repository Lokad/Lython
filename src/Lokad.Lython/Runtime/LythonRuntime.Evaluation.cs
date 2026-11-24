namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static void AppendListDisplayItem(
        PyList expanded,
        object value,
        bool isUnpacking,
        LythonSourceSpan itemSpan,
        LythonSourceSpan displaySpan,
        ExecutionContext context)
    {
        if (!isUnpacking)
        {
            AddListDisplayValue(expanded, value, displaySpan, context);
            return;
        }

        foreach (var item in PyIteration.Cursor.Create(value, itemSpan, context))
        {
            AddListDisplayValue(expanded, item, displaySpan, context);
        }
    }

    private static void AddListDisplayValue(
        PyList expanded,
        object value,
        LythonSourceSpan displaySpan,
        ExecutionContext context)
    {
        expanded.Add(RuntimeValue(value));
        context.CheckExecution(displaySpan);
    }

    private static void AppendTupleDisplayItem(
        List<object> expanded,
        MemoryGovernor.TemporaryMemoryReservation storage,
        object value,
        bool isUnpacking,
        LythonSourceSpan itemSpan,
        LythonSourceSpan displaySpan,
        ExecutionContext context)
    {
        if (!isUnpacking)
        {
            AddTupleDisplayValue(expanded, storage, value, displaySpan, context);
            return;
        }

        foreach (var item in PyIteration.Cursor.Create(value, itemSpan, context))
        {
            AddTupleDisplayValue(expanded, storage, item, displaySpan, context);
        }
    }

    private static void AddTupleDisplayValue(
        List<object> expanded,
        MemoryGovernor.TemporaryMemoryReservation storage,
        object value,
        LythonSourceSpan displaySpan,
        ExecutionContext context)
    {
        context.CheckExecution(displaySpan);
        if (expanded.Count == expanded.Capacity)
        {
            var oldCapacity = expanded.Capacity;
            var nextCapacity = oldCapacity == 0 ? 4 : checked(oldCapacity * 2);
            // Both backings coexist during growth; release the old estimate
            // only after replacement. Keep the new backing funded across awaits.
            storage.Grow(16L * nextCapacity, displaySpan);
            expanded.Capacity = nextCapacity;
            storage.Shrink(16L * oldCapacity);
        }
        expanded.Add(RuntimeValue(value));
    }

    private static PyTuple FinishTupleDisplay(List<object> expanded,
        MemoryGovernor.TemporaryMemoryReservation storage, LythonSourceSpan span, ExecutionContext context)
    {
        context.CheckExecution(span);
        if (expanded.Count == 0) return PyTuple.Empty;
        storage.Grow(PyTuple.EstimateApproximateBytes(expanded.Count), span);
        return PyTuple.FromReservedOwnedArray(expanded.ToArray(), context.MemoryGovernor, span, storage);
    }
}
