using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // One managed layout for final function arguments, locals and operands.
    // An unpublished numeric result has no guest identity yet. Object reads cache its
    // immutable box before any alias, protocol, collection or public boundary.
    internal struct ExecutableSlot
    {
        private static readonly object UnpublishedInteger = new();
        private static readonly object UnpublishedDouble = new();
        private object? _value;
        // Integer value, double bits, or an immutable integer tuple's item offset.
        private long _integer;

        internal object? Object
        {
            get
            {
                if (ReferenceEquals(_value, UnpublishedInteger)) return PublishInteger();
                if (ReferenceEquals(_value, UnpublishedDouble)) return PublishDouble();
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

        private object PublishDouble()
        {
            var value = (object)BitConverter.Int64BitsToDouble(_integer);
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

        internal void SetDouble(double value)
        {
            _value = UnpublishedDouble;
            _integer = BitConverter.DoubleToInt64Bits(value);
        }

        internal readonly bool TryGetDouble(out double value)
        {
            if (ReferenceEquals(_value, UnpublishedDouble))
            {
                value = BitConverter.Int64BitsToDouble(_integer);
                return true;
            }
            if (_value is double floating) { value = floating; return true; }
            value = 0;
            return false;
        }

        internal readonly bool TryGetInteger(out long value)
        {
            if (ReferenceEquals(_value, UnpublishedInteger)) { value = _integer; return true; }
            if (_integer != 0 && _value is PyTuple tuple) return tuple.TryReadInteger((int)(_integer - 1), out value);
            return TryMachineInteger(_value, out value);
        }

        internal readonly long ScalarStorageBytes => ReferenceEquals(_value, UnpublishedInteger)
            || ReferenceEquals(_value, UnpublishedDouble)
            || _integer != 0 && _value is PyTuple
            ? RuntimeMemoryEstimates.ScalarStorageBytes
            : RuntimeMemoryEstimates.EstimateScalarStorageBytes(_value);

        internal static ExecutableSlot FromObject(object? value) => new() { _value = value };

        internal static ExecutableSlot FromInteger(long value)
            => new() { _value = UnpublishedInteger, _integer = value };
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

    // Prove a closed hash/equality path without traversing composite keys or
    // invoking an earlier key's protocol out of order. Other keys hand off
    // before construction; both runners then use the same final-slot kernel.
    private static bool CanConstructClosedSlotKeys(ReadOnlySpan<ExecutableSlot> values, int stride,
        ExecutionContext context, LythonSourceSpan span)
    {
        for (var i = 0; i < values.Length; i += stride)
        {
            // Bound callbacks can borrow a larger admitted array than the
            // fixed small-call buffer. This proof must remain cancellable too.
            if (i != 0 && (i & 1023) == 0) context.CheckExecution(span);
            ref readonly var key = ref values[i];
            if (key.Reference is PyString or PyBytes or BigInteger or double or bool or PyNone
                || key.TryGetInteger(out _) || key.TryGetDouble(out _)) continue;
            return false;
        }
        return true;
    }

    private static PyDict CreateSlotDictionary(Span<ExecutableSlot> values,
        ExecutionContext context, LythonSourceSpan span)
    {
        // Pair count conservatively bounds final capacity, including duplicates.
        // Admission precedes allocation and key callbacks; no growth copies.
        var dictionary = new PyDict(values.Length / 2, context.MemoryGovernor, span);
        using var ambient = PyStructuralGuard.PushAmbient(context, span);
        try
        {
            for (var i = 0; i < values.Length; i += 2)
            {
                context.CheckExecution(span);
                var key = ValidateDictionaryKey(values[i].Object.RequireNotNull(), span);
                dictionary.SetItem(key, values[i + 1].Object.RequireNotNull());
            }
            context.CheckExecution(span);
            return dictionary;
        }
        catch { dictionary.DiscardUnpublishedStorage(); throw; }
    }

    private static PyDict CreateSlotRecord(Span<ExecutableSlot> values, PyTuple keys,
        ref ExecutableLookupCache cache, ExecutionContext context, LythonSourceSpan span)
    {
        var dictionary = cache.Target is PyDict.RecordKeys layout
            ? layout.CreateRecord(context, span, values.Length)
            : new PyDict(values.Length, context.MemoryGovernor, span);
        using var ambient = PyStructuralGuard.PushAmbient(context, span);
        try
        {
            for (var i = 0; i < values.Length; i++)
            {
                context.CheckExecution(span);
                dictionary.SetRecordItem((PyString)keys[i], values[i].Object.RequireNotNull(), context, span);
            }
            if (cache.Target is not PyDict.RecordKeys && dictionary.ShareRecordKeys(context, span) is { } shared)
                cache = new(shared, null);
            context.CheckExecution(span);
            return dictionary;
        }
        catch { dictionary.DiscardUnpublishedStorage(); throw; }
    }

    private static PySet CreateSlotSet(Span<ExecutableSlot> values,
        ExecutionContext context, LythonSourceSpan span)
    {
        var set = new PySet(context.MemoryGovernor, span);
        using var ambient = PyStructuralGuard.PushAmbient(context, span);
        try
        {
            set.EnsureCapacity(values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                context.CheckExecution(span);
                set.Add(ValidateSetItem(values[i].Object.RequireNotNull(), span));
            }
            context.CheckExecution(span);
            return set;
        }
        catch { set.ReleaseUnpublishedStorage(); throw; }
    }

}
