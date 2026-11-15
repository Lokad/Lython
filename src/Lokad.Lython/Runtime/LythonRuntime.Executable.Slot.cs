using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // One managed layout for final function arguments, locals and operands.
    // An unpublished integer has no guest identity yet. Object reads cache its
    // immutable box before any alias, protocol, collection or public boundary.
    internal struct ExecutableSlot
    {
        private static readonly object UnpublishedInteger = new();
        private object? _value;
        private long _integer;

        internal object? Object
        {
            get
            {
                if (ReferenceEquals(_value, UnpublishedInteger)) _value = (BigInteger)_integer;
                return _value;
            }
            set { _value = value; }
        }

        internal readonly bool IsUnassigned => _value is null;
        // Lifetime/type inspection must not create a guest box for dead values.
        internal readonly object? Reference => _value;

        internal void SetInteger(long value)
        {
            _value = UnpublishedInteger;
            _integer = value;
        }

        internal readonly bool TryGetInteger(out long value)
        {
            if (ReferenceEquals(_value, UnpublishedInteger)) { value = _integer; return true; }
            return TryMachineInteger(_value, out value);
        }

        internal readonly long ScalarStorageBytes => ReferenceEquals(_value, UnpublishedInteger)
            ? RuntimeMemoryEstimates.ScalarStorageBytes
            : RuntimeMemoryEstimates.EstimateScalarStorageBytes(_value);

        internal static ExecutableSlot FromObject(object? value) => new() { _value = value };
    }

    internal static long ExecutableSlotsBytes(int capacity) => checked(32L + 24L * capacity);

    private static long SlotTupleBytes(ReadOnlySpan<ExecutableSlot> values, ExecutionContext context,
        LythonSourceSpan span)
    {
        var bytes = PyTuple.EstimateApproximateBytes(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            bytes = checked(bytes + values[i].ScalarStorageBytes);
        }
        return bytes;
    }

    private static PyTuple CreateSlotTuple(Span<ExecutableSlot> values, ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = SlotTupleBytes(values, context, span);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var items = new object[values.Length];
        for (var i = 0; i < items.Length; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            items[i] = values[i].Object.RequireNotNull();
        }
        return PyTuple.FromReservedOwnedArray(items, context.MemoryGovernor, span, storage,
            bytes - PyTuple.EstimateApproximateBytes(values.Length));
    }

    private static PyList CreateSlotList(Span<ExecutableSlot> values, ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = PyList.EstimateCapacityBytes(values.Length);
        using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
        context.CheckExecution(span);
        var items = values.IsEmpty ? Array.Empty<object>() : new object[values.Length];
        for (var i = 0; i < items.Length; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            items[i] = values[i].Object.RequireNotNull();
        }
        storage.Commit(bytes);
        return PyList.FromTransferredArray(items, context.MemoryGovernor, span);
    }
}
