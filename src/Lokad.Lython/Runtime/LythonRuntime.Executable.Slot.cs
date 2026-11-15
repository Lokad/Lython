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
                if (ReferenceEquals(_value, UnpublishedInteger)) return PublishInteger();
                return _integer == 0 ? _value : PublishTupleBorrow();
            }
            set { _value = value; _integer = 0; }
        }

        internal readonly bool IsUnassigned => _value is null;
        // Lifetime/type inspection must not create a guest box for dead values.
        internal readonly object? Reference => _value;

        internal readonly bool IsUnpublishedInteger => ReferenceEquals(_value, UnpublishedInteger);

        private object PublishInteger()
        {
            var value = (object)(BigInteger)_integer;
            _value = value;
            _integer = 0;
            return value;
        }

        private object? PublishTupleBorrow()
        {
            if (_value is PyTuple tuple)
            {
                _value = tuple[(int)(_integer - 1)];
                _integer = 0;
            }
            return _value;
        }

        // Only immutable integer tuple items may be borrowed. The tuple owns
        // their eventual identity; copying this slot never copies an identity.
        internal void BorrowTupleInteger(PyTuple tuple, int index)
        {
            _value = tuple;
            _integer = (long)index + 1;
        }

        internal object PublishSharedInteger()
        {
            var value = _value;
            if (!ReferenceEquals(value, UnpublishedInteger)) return value.RequireNotNull();
            var boxed = (object)(BigInteger)_integer;
            var previous = Interlocked.CompareExchange(ref _value, boxed, UnpublishedInteger);
            return ReferenceEquals(previous, UnpublishedInteger) ? boxed : previous.RequireNotNull();
        }

        internal void SetInteger(long value)
        {
            _value = UnpublishedInteger;
            _integer = value;
        }

        internal readonly bool TryGetInteger(out long value)
        {
            if (ReferenceEquals(_value, UnpublishedInteger)) { value = _integer; return true; }
            if (_integer != 0 && _value is PyTuple tuple) return tuple.TryReadInteger((int)(_integer - 1), out value);
            return TryMachineInteger(_value, out value);
        }

        internal readonly long ScalarStorageBytes => ReferenceEquals(_value, UnpublishedInteger)
            || _integer != 0 && _value is PyTuple
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
        // Completed object references keep their compact backing and identities.
        // Fresh integer operands have no aliases yet; carry the final slots into
        // immutable tuple storage rather than boxing them during construction.
        var integers = !values.IsEmpty;
        for (var i = 0; i < values.Length && integers; i++)
        {
            if ((i & 1023) == 0) context.CheckExecution(span);
            integers = values[i].IsUnpublishedInteger;
        }
        if (integers) return PyTuple.FromIntegerSlots(values, context, span);
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
