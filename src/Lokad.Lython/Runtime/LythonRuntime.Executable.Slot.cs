using System.Buffers.Binary;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // One managed layout for final function arguments, locals and operands.
    // An unpublished scalar has no shared guest identity yet. Object reads cache
    // its immutable representation before a use can observe or copy it.
    internal struct ExecutableSlot
    {
        private static readonly object UnpublishedInteger = new();
        private static readonly object UnpublishedDouble = new();
        private object? _value;
        // An integer, double bits, short UTF-8 with its existing owner, a tuple
        // item offset, or a private-default marker with its admitted storage.
        private const long PrivateDefault = -1;
        private const long PrivateKeywordDefault = -2;
        private long _integer;

        internal object? Object
        {
            get
            {
                if (ReferenceEquals(_value, UnpublishedInteger)) return PublishInteger();
                if (ReferenceEquals(_value, UnpublishedDouble)) return PublishDouble();
                System.Diagnostics.Debug.Assert(!HasPrivateDefault && !HasPrivateKeywordDefault);
                return _integer == 0 ? _value : PublishDeferredValue();
            }
            set { _value = value; _integer = 0; }
        }

        internal readonly bool IsUnassigned => _value is null;
        // Lifetime/type inspection must not create a guest box for dead values.
        internal readonly object? Reference => _value;

        internal readonly bool IsUnpublishedInteger => ReferenceEquals(_value, UnpublishedInteger);
        private readonly bool HasPrivateDefault => _integer == PrivateDefault && _value is PyTuple or object?[] or PyDict;
        internal readonly bool HasPrivateKeywordDefault => _integer == PrivateKeywordDefault &&
            !IsUnpublishedInteger && !ReferenceEquals(_value, UnpublishedDouble);
        internal readonly bool RequiresRetirement => _value is PyIteration.Cursor or ExecutableTemporaryValue ||
            HasPrivateDefault || HasPrivateKeywordDefault;

        internal static ExecutableSlot OwnPrivateDefault(object storage) => new() { _value = storage, _integer = PrivateDefault };

        internal static ExecutableSlot OwnPrivateKeywordDefault(object value)
            => new() { _value = value, _integer = PrivateKeywordDefault };

        internal void RetireTemporary(MemoryGovernor governor)
        {
            if (_value is ExecutableTemporaryValue temporary)
            {
                // Clear before cleanup can reenter, as a returned loan can
                // immediately have another user.
                this = default;
                temporary.Dispose();
            }
            else if (HasPrivateDefault)
            {
                var storage = _value;
                this = default;
                if (storage is object?[] keywordValues) PyDict.RecordKeys.RefundPreparedValues(keywordValues, governor);
                else governor.RefundUnpublishedValue(storage!);
            }
            else if (HasPrivateKeywordDefault)
            {
                this = default;
                governor.Release(PyDict.RecordKeys.SinglePreparedValueBytes);
            }
        }

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

        // Seven bytes plus the length fit in the existing scalar word.
        internal const int ShortUtf8Capacity = sizeof(long) - 1;

        internal static ExecutableSlot FromShortUtf8(ReadOnlySpan<byte> source, MemoryGovernor owner)
        {
            System.Diagnostics.Debug.Assert(source.Length is > 0 and <= ShortUtf8Capacity);
            var packed = (long)source.Length << 56;
            for (var i = 0; i < source.Length; i++)
                packed |= (long)source[i] << (i * 8);
            return new() { _value = owner, _integer = packed };
        }

        internal readonly bool TryGetShortUtf8(out long packed)
        {
            packed = _integer;
            return packed > 0 && _value is MemoryGovernor;
        }

        internal static ReadOnlySpan<byte> UnpackShortUtf8(long packed, Span<byte> buffer)
        {
            BinaryPrimitives.WriteInt64LittleEndian(buffer, packed);
            return buffer[..(int)(packed >> 56)];
        }

        private object? PublishDeferredValue()
        {
            if (_value is MemoryGovernor owner)
            {
                Span<byte> buffer = stackalloc byte[sizeof(long)];
                var bytes = UnpackShortUtf8(_integer, buffer);
                // The CSV record already admitted this possible publication.
                var text = PyString.CreateReservedUtf8(bytes.Length, owner);
                bytes.CopyTo(text.UnpublishedUtf8);
                text.CompleteUnpublishedUtf8(bytes.Length);
                Object = text;
            }
            else if (_integer > 0 && _value is PyTuple tuple)
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
            if (_integer > 0 && _value is PyTuple tuple) return tuple.TryReadInteger((int)(_integer - 1), out value);
            return TryMachineInteger(_value, out value);
        }

        internal readonly bool TryGetNumber(out PyNumber value)
        {
            if (ReferenceEquals(_value, UnpublishedDouble))
            {
                value = PyNumber.FromFloat(BitConverter.Int64BitsToDouble(_integer));
                return true;
            }
            if (IsUnpublishedInteger)
            {
                value = PyNumber.FromInteger(_integer);
                return true;
            }
            if (_integer > 0 && _value is PyTuple tuple && tuple.TryReadInteger((int)(_integer - 1), out var integer))
            {
                value = PyNumber.FromInteger(integer);
                return true;
            }
            return PyNumberOps.TryAsNumber(_value!, out value);
        }

        internal readonly long ScalarStorageBytes => ReferenceEquals(_value, UnpublishedInteger)
            || ReferenceEquals(_value, UnpublishedDouble)
            || _integer > 0 && _value is PyTuple
            ? RuntimeMemoryEstimates.ScalarStorageBytes
            : RuntimeMemoryEstimates.EstimateScalarStorageBytes(_value);

        internal static ExecutableSlot FromObject(object? value) => new() { _value = value };

        internal static ExecutableSlot FromInteger(long value)
            => new() { _value = UnpublishedInteger, _integer = value };
    }

    internal static long ExecutableSlotsBytes(int capacity) => checked(32L + 24L * capacity);

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

    private static PyDict CreateSlotDictionary(Span<ExecutableSlot> inputs, ref ExecutableLookupCache cache,
        ExecutionContext context, LythonSourceSpan span)
    {
        if (cache.Target is PyDict.RecordKeys keys && keys.TryCreateDisplay(inputs, context, span) is { } record)
            return record;
        var dictionary = CreateSlotDictionary(inputs, context, span);
        try
        {
            // The general build preserves protocol order and first-key identity.
            // A duplicate display has no one-to-one key/value layout to reuse.
            if (dictionary.Count == inputs.Length / 2) CacheRecordKeys(ref cache, dictionary, context, span);
            return dictionary;
        }
        catch { dictionary.DiscardUnpublishedStorage(); throw; }
    }

    private static void CacheRecordKeys(ref ExecutableLookupCache cache, PyDict dictionary,
        ExecutionContext context, LythonSourceSpan span)
    {
        // Only key storage moves to the cache owner. A callback during
        // admission may fill the same site before the first row completes.
        if (cache.Target is null && dictionary.ShareRecordKeys(context, span) is { } shared && cache.Target is null)
            cache = new(shared, null);
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
