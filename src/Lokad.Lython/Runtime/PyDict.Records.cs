using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict
{
    private RecordKeys? _recordKeys;
    internal bool HasSharedRecordKeys => _recordKeys is not null;

    internal bool TryGetDefinitionValue(PyString key, int position, [MaybeNullWhen(false)] out object value,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // Probe the live record only after admission and argument evaluation.
        // A value assignment keeps its slot. Deletion, promotion or replacement
        // can invalidate this proof, so fall through to the ordinary lookup.
        if (_recordKeys is not null && (uint)position < (uint)NativeExtent &&
            ReferenceEquals(_nativeItems![position].Key, key) && _nativeValues![position] is { } current)
        {
            context.CheckExecution(span);
            value = current;
            return true;
        }
        return TryGetValue(key, out value, context, span);
    }

    // Layout identities survive changes to values, but not insertion of new
    // keys. Deletion leaves a vacant slot. Read the current value only after
    // checking both facts; a cache entry must never keep a previous row alive.
    internal bool TryReadRecordValue(PyString key, ref LythonRuntime.ExecutableLookupCache cached,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span,
        [MaybeNullWhen(false)] out object value)
    {
        context.CheckExecution(span);
        if (_recordKeys is not { } layout)
        {
            value = null;
            return false;
        }
        int index;
        if (ReferenceEquals(cached.Target, layout) && ReferenceEquals(cached.Value, key)) index = cached.Position;
        else
        {
            var hash = MixNativeHash(LythonRuntime.ComputeStructuralHashCode(key, span, context));
            index = FindNativeIndex(key, hash, context, span);
            if (index < 0)
            {
                value = null;
                return false;
            }
            cached = new(layout, key, index);
        }
        value = IsNativeLive(index) ? _nativeValues![index] : null;
        return value is not null;
    }

    // A layout retains keys only, never its originating row or values. Funding
    // belongs to the existing execution/cache owner, including superseded layouts
    // that surviving rows can still use. There is no separate allowance/refcount.
    internal sealed class RecordKeys
    {
        private readonly NativeKey[] _keys;
        // A compiled layout is funded by its code owner. Dynamic layouts
        // stay with the execution that established them.
        private readonly MemoryGovernor? _owner;
        private readonly long _sequence;
        internal int Count { get; }

        internal RecordKeys(PyDict dictionary)
            : this(dictionary._nativeItems!, dictionary._memoryGovernor!, dictionary._nativeSequence,
                dictionary._nativeCount)
        { }

        private RecordKeys(NativeKey[] keys, MemoryGovernor? owner, long sequence, int count)
        {
            _keys = keys;
            _owner = owner;
            _sequence = sequence;
            Count = count;
        }

        // Definition names are distinct native strings in their final insertion order.
        // Build the shared table directly, with no first record to freeze or detach.
        internal static RecordKeys? PrepareDefinition<T>(int count, T names, Func<T, int, PyString> nameAt,
            CancellationToken token, LythonSourceSpan span)
        {
            if (count == 0) return null;
            // The enclosing compilation was admitted before preparing code.
            var keys = new NativeKey[NativeCapacity(count)];
            for (var i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                var key = nameAt(names, i);
                var hash = MixNativeHash(PyValueComparer.NormalizeHash(key.GetHashCode(token, span)));
                LinkNativeKey(keys, i, new NativeKey { Key = key, Hash = hash, Sequence = i + 1 });
            }
            return new RecordKeys(keys, null, count, count);
        }

        internal PyDict CreateRecord(LythonRuntime.ExecutionContext context, LythonSourceSpan span, int initialCapacity)
        {
            context.CheckExecution(span);
            var owner = context.MemoryGovernor;
            if (_owner is not null && !ReferenceEquals(_owner, owner))
                throw new InvalidOperationException("Record keys must retain their execution owner.");
            var capacity = NativeCapacity(initialCapacity);
            var bytes = RecordBytes(capacity);
            owner.Reserve(bytes, span);
            try
            {
                context.CheckExecution(span);
                var record = new PyDict(_keys, this, _sequence, capacity, owner, span)
                {
                    _nativeCommittedBytes = bytes,
                };
                owner.Commit(bytes);
                return record;
            }
            catch { owner.ReleaseReserved(bytes); throw; }
        }

        internal int PreparedValueCapacity => NativeCapacity(Count);

        internal object?[] CreatePreparedValues(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            System.Diagnostics.Debug.Assert(_owner is null);
            var capacity = PreparedValueCapacity;
            var bytes = RecordBytes(capacity);
            var owner = context.MemoryGovernor;
            owner.Reserve(bytes, span);
            try
            {
                context.CheckExecution(span);
                var values = new object?[capacity];
                owner.Commit(bytes);
                return values;
            }
            catch { owner.ReleaseReserved(bytes); throw; }
        }

        internal PyDict? TryCreateDisplay(Span<LythonRuntime.ExecutableSlot> inputs,
            LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            if (!ReferenceEquals(_owner, context.MemoryGovernor) || Count != inputs.Length / 2) return null;
            for (var i = 0; i < Count; i++)
            {
                context.CheckExecution(span);
                if (!ReferenceEquals(_keys[i].Key, inputs[2 * i].Reference)) return null;
            }

            // Already-evaluated operands survive admission and cannot be
            // replaced by reentrant changes to the originating local names.
            var record = CreateRecord(context, span, Count);
            try
            {
                for (var i = 0; i < Count; i++)
                {
                    context.CheckExecution(span);
                    record._nativeValues![i] = inputs[2 * i + 1].Object.RequireNotNull();
                }
                context.CheckExecution(span);
                record._nativeCount = Count;
                return record;
            }
            catch { record.DiscardUnpublishedStorage(); throw; }
        }

        // The caller admitted both the values and eventual dictionary shell.
        // Hold only the values until Python reads __annotations__.
        internal object?[] CopyAnnotationValues(ReadOnlySpan<LythonRuntime.ExecutableSlot> evaluated,
            int capacity, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            System.Diagnostics.Debug.Assert(_owner is null && Count != 0 && evaluated.Length == Count && capacity == PreparedValueCapacity);
            context.CheckExecution(span);
            var values = new object?[capacity];
            for (var i = 0; i < evaluated.Length; i++)
            {
                if (i != 0 && (i & 63) == 0) context.CheckExecution(span);
                values[i] = LythonRuntime.RuntimeValue(evaluated[i].Object.RequireNotNull());
            }
            context.CheckExecution(span);
            return values;
        }

        internal PyDict AdoptPreparedValues(object?[] values, MemoryGovernor owner, LythonSourceSpan? span)
            => new(_keys, this, _sequence, 0, owner, span)
            {
                _nativeValues = values,
                _nativeCount = Count,
                _nativeCommittedBytes = RecordBytes(values.Length),
            };

        internal static void RefundPreparedValues(object?[] values, MemoryGovernor owner)
            => owner.Release(RecordBytes(values.Length));

        internal static long RecordBytes(int capacity)
            => checked(NativeShellBytes + (capacity == 0 ? 0 : 32L + 72L * capacity));

        // The caller transfers an exclusively owned, already-funded CSV row.
        // Preserve actual key identities, not only equivalent header text.
        internal PyDict? TryAdoptRecord(PyList fields, object[] values,
            LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if (_owner is null || !ReferenceEquals(_owner, context.MemoryGovernor))
                throw new InvalidOperationException("Record keys must retain their execution owner.");
            if (!MatchesFields()) return null;
            using var shell = _owner.ReserveTemporary(NativeShellBytes, span);
            context.CheckExecution(span);
            // Admission can mutate headers or reenter the reader. No callback
            // or further admission occurs between this proof and publication.
            if (!MatchesFields()) return null;
            var record = new PyDict(_keys, this, _sequence, 0, _owner, span)
            {
                _nativeValues = values,
                _nativeCount = values.Length,
                // Retain the row's original conservative headroom. Its storage
                // is transferred, never refunded and then reserved again.
                _nativeCommittedBytes = checked(NativeShellBytes + PyList.EstimateCapacityBytes(values.Length)),
            };
            shell.Commit(NativeShellBytes);
            return record;

            bool MatchesFields()
            {
                if (fields.Count != Count || values.Length != Count) return false;
                for (var i = 0; i < Count; i++)
                {
                    context.CheckExecution(span);
                    if (!ReferenceEquals(_keys[i].Key, fields[i])) return false;
                }
                return true;
            }
        }

        internal PyString? MatchDelimitedName(ReadOnlySpan<byte> source, int position, byte delimiter,
            LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            if ((uint)position >= (uint)Count) return null;
            var key = (PyString)_keys[position].Key!;
            var length = key.ByteLength;
            return length < source.Length && source[length] == delimiter &&
                EqualNativeText(key, source[..length], context, span) ? key : null;
        }

        internal PyString? FindName(ReadOnlySpan<byte> bytes, int position,
            LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if ((uint)position < (uint)Count && EqualNativeText((PyString)_keys[position].Key!, bytes, context, span))
                return (PyString)_keys[position].Key!;
            var hash = MixNativeHash(PyValueComparer.NormalizeHash(PyString.HashUtf8(bytes, context, span)));
            var index = FindNativeTextIndex(_keys, hash, bytes, context, span);
            return index < 0 ? null : (PyString)_keys[index].Key!;
        }
    }

    private PyDict(NativeKey[] keys, RecordKeys layout, long sequence, int capacity, MemoryGovernor owner, LythonSourceSpan? span)
    {
        _memoryGovernor = owner; _allocationSpan = span; _items = this;
        _nativeItems = keys; _recordKeys = layout; _nativeSequence = sequence;
        _nativeValues = capacity == 0 ? [] : new object?[capacity];
    }

    internal RecordKeys? ShareRecordKeys(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (!ReferenceEquals(_memoryGovernor, context.MemoryGovernor)) return null;
        if (_nativeCount == 0) return null;
        if (_recordKeys is { } shared && shared.Count == _nativeCount) return shared;
        if (!CanShareRecordKeys(context, span)) return null;
        if (_recordKeys is not null) EnsureNativeCapacity(_nativeCount, _memoryGovernor, span);
        using var storage = context.MemoryGovernor.ReserveTemporary(64, span);
        context.CheckExecution(span);
        // Admission can alter reachable data. Reuse a layout already established
        // by that callback, or validate the current private keys before freezing.
        if (_recordKeys is { } current) return current;
        if (!CanShareRecordKeys(context, span)) return null;
        MaterializeNativeValues(span);
        var layout = new RecordKeys(this);
        _recordKeys = layout;
        // Transfer the existing key funding to the batch/cache lifetime without
        // refunding the borrowed root. Rows own only their shell/value capacity.
        _nativeCommittedBytes -= NativeKeyStorageBytes(_nativeItems!.Length);
        storage.Commit(64);
        return layout;
    }

    private bool CanShareRecordKeys(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_nativeItems is null || _keysOnly || HasProtocolKeys || _nativeCount == 0) return false;
        for (var i = 0; i < NativeExtent; i++)
        {
            context.CheckExecution(span);
            if (IsNativeLive(i) && _nativeItems[i].Key is not PyString) return false;
        }
        return true;
    }

    internal void AddDefinitionValue(PyString key, object value, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (_recordKeys is null)
        {
            TryAddNativeString(key, value, context, span);
            return;
        }
        System.Diagnostics.Debug.Assert(_nativeCount < _recordKeys.Count &&
            ReferenceEquals(_nativeItems![_nativeCount].Key, key));
        _nativeValues![_nativeCount++] = value;
    }

    internal void SetRecordItem(PyString key, object value, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span)
    {
        context.CheckExecution(span);
        // Builders call this only before publishing their record. Ordered
        // prefixes fill directly; duplicates and mismatches use normal mutation.
        if (_recordKeys is not null && _nativeCount < _recordKeys.Count &&
            ReferenceEquals(_nativeItems![_nativeCount].Key, key))
        {
            EnsureRecordValueCapacity(checked(_nativeCount + 1), context, span);
            if (_nativeValues![_nativeCount] is null)
            {
                _nativeValues[_nativeCount++] = value;
                _version++;
                return;
            }
        }
        SetItem(key, value);
    }

    private void EnsureRecordValueCapacity(int targetCount, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (targetCount <= _nativeValues!.Length) return;
        // The ordered prefix has outgrown its backing. Its remaining keys are
        // already immutable, so admit the layout's final value capacity once.
        var capacity = NativeCapacity(_recordKeys!.Count);
        using var replacement = _memoryGovernor!.ReserveTemporary(NativeValueStorageBytes(capacity), span);
        context.CheckExecution(span);
        var previous = _nativeValues;
        var values = new object?[capacity];
        for (var offset = 0; offset < previous.Length; offset += 4096)
        {
            context.CheckExecution(span);
            previous.AsSpan(offset, Math.Min(4096, previous.Length - offset)).CopyTo(values.AsSpan(offset));
        }
        _nativeValues = values;
        var bytes = NativeValueStorageBytes(capacity) - NativeValueStorageBytes(previous.Length);
        replacement.Commit(bytes);
        _nativeCommittedBytes += bytes;
    }

    private void PrepareNativeKeyMutation(object key, uint hash, bool protocol)
    {
        // Promote before any guest equality work. Exact native keys can prove an
        // existing value update without admission; an insertion needs private keys.
        if (_recordKeys is not null && (protocol || FindNativeIndex(key, hash) < 0))
            EnsureNativeCapacity(checked(_nativeCount + 1), _memoryGovernor, QuerySpan);
    }
}
