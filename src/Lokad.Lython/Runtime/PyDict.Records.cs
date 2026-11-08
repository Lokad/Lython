using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict
{
    private RecordKeys? _recordKeys;

    // A layout retains keys only, never its originating row or values. Funding
    // belongs to the existing execution/cache owner, including superseded layouts
    // that surviving rows can still use. There is no separate allowance/refcount.
    internal sealed class RecordKeys
    {
        private readonly NativeKey[] _keys;
        private readonly MemoryGovernor _owner;
        private readonly long _sequence;
        internal int Count { get; }

        internal RecordKeys(PyDict dictionary)
        {
            _keys = dictionary._nativeItems!;
            _owner = dictionary._memoryGovernor!;
            _sequence = dictionary._nativeSequence;
            Count = dictionary._nativeCount;
        }

        internal PyDict CreateRecord(LythonRuntime.ExecutionContext context, LythonSourceSpan span, int initialCapacity)
        {
            context.CheckExecution(span);
            if (!ReferenceEquals(_owner, context.MemoryGovernor))
                throw new InvalidOperationException("Record keys must retain their execution owner.");
            var capacity = NativeCapacity(initialCapacity);
            var bytes = checked(NativeShellBytes + (capacity == 0 ? 0 : 32L + 72L * capacity));
            using var storage = _owner.ReserveTemporary(bytes, span);
            context.CheckExecution(span);
            var record = new PyDict(_keys, this, _sequence, capacity, _owner, span);
            storage.Commit(bytes);
            record._nativeCommittedBytes = bytes;
            return record;
        }

        // The caller transfers an exclusively owned, already-funded CSV row.
        // Preserve actual key identities, not only equivalent header text.
        internal PyDict? TryAdoptRecord(PyList fields, object[] values,
            LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if (!ReferenceEquals(_owner, context.MemoryGovernor))
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

        internal PyString? FindName(ReadOnlySpan<byte> bytes, int position,
            LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if ((uint)position < (uint)Count && EqualName((PyString)_keys[position].Key!, bytes, context, span))
                return (PyString)_keys[position].Key!;
            var hash = MixNativeHash(PyValueComparer.NormalizeHash(PyString.HashUtf8(bytes, context, span)));
            for (var i = _keys[NativeBucket(hash, _keys.Length)].Bucket - 1; i >= 0; i = _keys[i].Next - 1)
            {
                context.CheckExecution(span);
                if (_keys[i].Hash == hash && EqualName((PyString)_keys[i].Key!, bytes, context, span))
                    return (PyString)_keys[i].Key!;
            }
            return null;
        }

        private static bool EqualName(PyString name, ReadOnlySpan<byte> bytes,
            LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        {
            var other = name.Utf8Span;
            if (other.Length != bytes.Length) return false;
            for (var offset = 0; offset < bytes.Length;)
            {
                context.CheckExecution(span);
                var count = Math.Min(4096, bytes.Length - offset);
                if (!bytes.Slice(offset, count).SequenceEqual(other.Slice(offset, count))) return false;
                offset += count;
            }
            return true;
        }
    }

    private PyDict(NativeKey[] keys, RecordKeys layout, long sequence, int capacity, MemoryGovernor owner, LythonSourceSpan span)
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

    internal void SetRecordItem(PyString key, object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
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
        var capacity = NativeCapacity(targetCount);
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
