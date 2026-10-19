using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyList : IMutablePySequenceValue, IMutablePyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    private IPyListStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private long _numericStorageBytes;


    public PyList()
    {
        _items = PyListStorage.Create();
    }

    public PyList(IEnumerable<object> items)
    {
        _items = PyListStorage.Create(items);
    }

    public PyList(IEnumerable<object> items, MemoryGovernor governor) : this(items, governor, null) { }

    // The native constructor publishes this owner before it starts consuming
    // arbitrary iterables. Known capacity keeps the existing upfront preflight.
    internal PyList(int capacity, MemoryGovernor governor, LythonSourceSpan allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = capacity <= PyListStorage.SmallCapacity
            ? new SmallPyListStorage(governor, allocationSpan)
            : new ArrayPyListStorage(capacity, governor, allocationSpan);
    }

    // Only a fresh constructor result that has never been exposed or registered.
    // Drop its actual backing before refunding; guest values keep their owners.
    internal void DiscardUnpublishedStorage()
    {
        var charge = _items.ReleaseCommittedBytes() + _numericStorageBytes;
        _items = null!;
        _numericStorageBytes = 0;
        _memoryGovernor!.Release(charge);
    }

    public PyList(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyListStorage.Create(items, governor, allocationSpan);
        ChargeInitialNumericStorage();
    }

    public PyList(PyList other)
    {
        _memoryGovernor = other._memoryGovernor;
        _allocationSpan = other._allocationSpan;
        _items = other._memoryGovernor is null
            ? other._items.Clone()
            : PyListStorage.Create(other._items, other._memoryGovernor, other._allocationSpan);
        if (_memoryGovernor is not null)
        {
            ChargeInitialNumericStorage();
        }
    }

    public int Count => _items.Count;

    // Current backing funding for unpublished rollback and explicit replacement.
    // Numeric slot estimates share the batch owner; reference withdrawal does not refund them.
    internal long CommittedStorageBytes => _items.CommittedBytes + _numericStorageBytes;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public int Length => Count;

    public object this[int index]
    {
        get => _items[index];
        set => SetElement(index, value);
    }

    public void Add(object value)
    {
        var committedBefore = CommittedStorageBytes;
        var target = checked(_numericStorageBytes + NumericBytes(value));
        var growth = ReserveNumericGrowth(target);
        try
        {
            _items = PyListStorage.EnsureCapacity(_items, checked(Count + 1), _memoryGovernor, _allocationSpan);
        }
        catch
        {
            if (growth > 0) _memoryGovernor!.Release(growth);
            throw;
        }
        _items.Add(value);
        CompleteNumericChange(target, committedBefore);
    }

    // Numeric slots conservatively cover small boxes without consulting identity
    // or payload tracking. Large magnitudes keep their independent actual owner.
    private long NumericBytes(object value) =>
        _memoryGovernor is null ? 0 : RuntimeMemoryEstimates.EstimateNumericSlotBytes(value);

    private long NumericBytes(IEnumerable<object> values)
    {
        if (_memoryGovernor is null) return 0;
        long bytes = 0;
        foreach (var value in values) bytes = checked(bytes + NumericBytes(value));
        return bytes;
    }

    // Publish the new subtotal only after mutation. A backing-growth failure
    // refunds this positive delta; no identities or rollback records are needed.
    private long ReserveNumericGrowth(long target)
    {
        var growth = Math.Max(target - _numericStorageBytes, 0);
        if (growth > 0)
        {
            _memoryGovernor!.Reserve(growth, _allocationSpan);
            _memoryGovernor.Commit(growth);
        }
        return growth;
    }

    private void CompleteNumericChange(long target, long committedBefore)
    {
        var released = _numericStorageBytes - target;
        _numericStorageBytes = target;
        if (released > 0) _memoryGovernor!.Release(released);
    }

    private void ChargeInitialNumericStorage()
    {
        try
        {
            var target = NumericBytes((IEnumerable<object>)_items);
            ReserveNumericGrowth(target);
            _numericStorageBytes = target;
        }
        catch
        {
            _memoryGovernor!.Release(_items.ReleaseCommittedBytes());
            throw;
        }
    }

    public void AddRange(IEnumerable<object> values)
    {
        if (values is PyIteration.CheckedSequence { Source: PyList or IPyListStorage or IReadOnlyCollection<object> } checkedSequence)
        {
            values = checkedSequence.Source;
        }

        if (values is PyList sourceList)
        {
            // Snapshot the source (which may be this list) so extending appends
            // the original elements; the copy itself is budget-checked.
            AddRange(sourceList.ToArray());
            return;
        }

        if (values is IPyListStorage sourceStorage)
        {
            AddRange(sourceStorage.ToArray());
            return;
        }

        // List-backed inputs are snapshotted above (PyList implements this interface
        // through its sequence interfaces, so it must never reach this live count).
        if (values is IReadOnlyCollection<object> known)
        {
            // Bounded, known-size inputs reserve exactly once up front, so the
            // bulk append below cannot grow past the ensured capacity uncharged.
            var committedBefore = CommittedStorageBytes;
            var target = checked(_numericStorageBytes + NumericBytes((IEnumerable<object>)known));
            var growth = ReserveNumericGrowth(target);
            try
            {
                _items = PyListStorage.EnsureCapacity(_items, checked(Count + known.Count), _memoryGovernor, _allocationSpan);
            }
            catch (Exception)
            {
                if (growth > 0) _memoryGovernor!.Release(growth);
                throw;
            }

            foreach (var value in known)
            {
                _items.Add(value);
            }

            CompleteNumericChange(target, committedBefore);
            return;
        }

        try
        {
            // Unknown-length inputs stream incrementally: growth is charged per
            // item, so a failing iterable keeps its partial effects and an
            // unbounded one meets the memory budget instead of hanging.
            foreach (var value in values)
            {
                Add(value);
            }
        }
        catch (InvalidOperationException)
        {
            // Iteration observed our own concurrent growth (for example extending
            // with a live iterator over this list). That cannot terminate like
            // CPython does, so fail explicitly instead of leaking the CLR error.
            throw RuntimeErrors.Runtime("list modified during extension.", _allocationSpan);
        }
    }

    internal void AddRange(IEnumerable<object> values, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (values is PyIteration.CheckedSequence { Source: PyList or IPyListStorage or IReadOnlyCollection<object> } checkedSequence)
        {
            values = checkedSequence.Source;
        }

        if (values is IReadOnlyCollection<object> || values is PyList || values is IPyListStorage)
        {
            AddRange(values);
            context.ObserveCollectionCount(Count, span);
            return;
        }

        try
        {
            var added = 0;
            foreach (var value in values)
            {
                Add(value);
                context.ObserveCollectionCount(Count, span);
                if ((++added & 63) == 0)
                {
                    context.CheckExecution(span);
                }
            }
        }
        catch (InvalidOperationException)
        {
            throw RuntimeErrors.Runtime("list modified during extension.", span);
        }
    }

    internal async ValueTask AddRangeAsync(object source, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // In-memory fast paths ride the sync bulk path unchanged (no suspension
        // possible, self-extension stays snapshotted); only lazy sources await.
        if (source is PyList || source is IPyListStorage || source is IReadOnlyCollection<object>)
        {
            AddRange(LythonRuntime.ToSequence(source, span, context), context, span);
            return;
        }

        try
        {
            var added = 0;
            await foreach (var value in LythonRuntime.ToSequenceAsync(source, span, context).ConfigureAwait(false))
            {
                Add(value);
                context.ObserveCollectionCount(Count, span);
                if ((++added & 63) == 0)
                {
                    context.CheckExecution(span);
                }
            }
        }
        catch (InvalidOperationException)
        {
            throw RuntimeErrors.Runtime("list modified during extension.", span);
        }
    }

    public void Insert(int index, object value)
    {
        var normalized = index < 0 ? index + Count : index;
        normalized = Math.Clamp(normalized, 0, Count);
        var committedBefore = CommittedStorageBytes;
        var target = checked(_numericStorageBytes + NumericBytes(value));
        var growth = ReserveNumericGrowth(target);
        try
        {
            _items = PyListStorage.EnsureCapacity(_items, checked(Count + 1), _memoryGovernor, _allocationSpan);
        }
        catch
        {
            if (growth > 0) _memoryGovernor!.Release(growth);
            throw;
        }
        _items.InsertAt(normalized, value);
        CompleteNumericChange(target, committedBefore);
    }

    public void Reverse()
    {
        // Swap in place: the count never changes, so no charging is needed.
        var count = Count;
        for (var i = 0; i < count / 2; i++)
        {
            var opposite = count - 1 - i;
            var saved = _items[i];
            _items[i] = _items[opposite];
            _items[opposite] = saved;
        }
    }

    public void ReplaceAll(IEnumerable<object> values)
    {
        ReplaceStorage(values);
    }

    public void RepeatInPlace(int count, LythonSourceSpan span)
    {
        if (count <= 0 || Count == 0)
        {
            ReplaceStorage([]);
            return;
        }
        var totalLength = (long)Count * count;
        if (totalLength > int.MaxValue)
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        var total = (int)totalLength;
        var originalCount = Count;
        var committedBefore = CommittedStorageBytes;
        var target = checked(_numericStorageBytes * count);
        var growth = ReserveNumericGrowth(target);
        try
        {
            _items = PyListStorage.EnsureCapacity(_items, total, _memoryGovernor, _allocationSpan);
        }
        catch
        {
            if (growth > 0) _memoryGovernor!.Release(growth);
            throw;
        }
        _items.RepeatFill(originalCount, total);
        CompleteNumericChange(target, committedBefore);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (_memoryGovernor is null)
        {
            long bytes = 0;
            foreach (var value in _items)
                bytes = checked(bytes + RuntimeMemoryEstimates.EstimateNumericSlotBytes(value));
            governor.Reserve(bytes, allocationSpan);
            governor.Commit(bytes);
            _numericStorageBytes = bytes;
            _memoryGovernor = governor;
        }
        _allocationSpan ??= allocationSpan;
    }

    public void RemoveAt(int index)
    {
        var committedBefore = CommittedStorageBytes;
        var target = _numericStorageBytes - NumericBytes(_items[index]);
        _items.RemoveAt(index);
        CompleteNumericChange(target, committedBefore);
    }

    public void RemoveRange(int index, int count)
    {
        if (count <= 0) return;
        var committedBefore = CommittedStorageBytes;
        long outgoing = 0;
        // Read before mutation both to validate the range and to count its slots.
        for (var i = 0; i < count; i++) outgoing += NumericBytes(_items[index + i]);
        _items.RemoveRangeAt(index, count);
        CompleteNumericChange(_numericStorageBytes - outgoing, committedBefore);
    }

    public void DeleteSlice(PyIndexing.SliceBounds bounds)
    {
        if (bounds.Step == 1)
        {
            RemoveRange(bounds.Start, Math.Max(bounds.End - bounds.Start, 0));
            return;
        }
        if (bounds.Count == 0) return;
        var committedBefore = CommittedStorageBytes;
        var count = Count;
        var destination = 0;
        long outgoing = 0;
        for (var source = 0; source < count; source++)
        {
            if (bounds.Contains(source)) outgoing += NumericBytes(_items[source]);
            else
            {
                if (destination != source) _items[destination] = _items[source];
                destination++;
            }
        }
        if (destination < count) _items.RemoveRangeAt(destination, count - destination);
        CompleteNumericChange(_numericStorageBytes - outgoing, committedBefore);
    }

    public void SetSlice(PyIndexing.SliceBounds bounds, IReadOnlyList<object> values, LythonSourceSpan span)
    {
        if (bounds.Step == 1)
        {
            var removeCount = Math.Max(bounds.End - bounds.Start, 0);
            IReadOnlyList<object> staged = values;
            if (values is PyList sourceList) staged = sourceList.ToArray();
            else if (values is IPyListStorage sourceStorage) staged = sourceStorage.ToArray();
            var committedBefore = CommittedStorageBytes;
            long outgoing = 0;
            for (var i = 0; i < removeCount; i++) outgoing += NumericBytes(_items[bounds.Start + i]);
            var target = checked(_numericStorageBytes - outgoing + NumericBytes((IEnumerable<object>)staged));
            var growth = ReserveNumericGrowth(target);
            try
            {
                _items = PyListStorage.EnsureCapacity(_items, checked(Count - removeCount + staged.Count), _memoryGovernor, _allocationSpan);
            }
            catch
            {
                if (growth > 0) _memoryGovernor!.Release(growth);
                throw;
            }
            _items.ReplaceRange(bounds.Start, removeCount, staged);
            CompleteNumericChange(target, committedBefore);
            return;
        }
        if (bounds.Count != values.Count)
            throw new LythonRuntimeException("ValueError", "attempt to assign sequence of size " + values.Count + " to extended slice of size " + bounds.Count, span);
        var extendedBefore = CommittedStorageBytes;
        long extendedOutgoing = 0;
        foreach (var index in bounds.Indices()) extendedOutgoing += NumericBytes(_items[index]);
        var extendedTarget = checked(_numericStorageBytes - extendedOutgoing + NumericBytes((IEnumerable<object>)values));
        ReserveNumericGrowth(extendedTarget);
        var position = 0;
        foreach (var index in bounds.Indices()) _items[index] = values[position++];
        CompleteNumericChange(extendedTarget, extendedBefore);
    }

    public void Clear()
    {
        if (_memoryGovernor is not null)
        {
            // Admit replacement while the old backing remains retained. A
            // refusal must leave both contents and their funding unchanged.
            var replacement = PyListStorage.Create(System.Array.Empty<object>(), _memoryGovernor, _allocationSpan);
            var released = _items.ReleaseCommittedBytes();
            _items = replacement;
            if (released > 0)
            {
                _memoryGovernor.Release(released);
            }

            _memoryGovernor.Release(_numericStorageBytes);
            _numericStorageBytes = 0;
            return;
        }

        _items.Clear();
    }

    public object GetItem(int index) => _items[index];

    public object CreateSlice(IEnumerable<object> items) => _memoryGovernor is null
        ? new PyList(items)
        : new PyList(items, _memoryGovernor, _allocationSpan);

    public void SetItem(int index, object value) => SetElement(index, value);

    public object GetIndex(int index) => _items[index];

    public object GetSlice(IEnumerable<int> indices) => _memoryGovernor is null
        ? new PyList(PySequenceMaterialization.MaterializeSlice(_items, indices, null, _allocationSpan))
        : new PyList(PySequenceMaterialization.MaterializeSlice(_items, indices, _memoryGovernor, _allocationSpan), _memoryGovernor, _allocationSpan);

    public void SetIndex(int index, object value) => SetElement(index, value);

    // Transfer an existing numeric slot allowance; reserve a positive change
    // before the store and release a negative change afterwards.
    private void SetElement(int index, object value)
    {
        var outgoing = _items[index];
        if (ReferenceEquals(outgoing, value)) return;
        var committedBefore = CommittedStorageBytes;
        var target = _numericStorageBytes - NumericBytes(outgoing) + NumericBytes(value);
        ReserveNumericGrowth(target);
        _items[index] = value;
        CompleteNumericChange(target, committedBefore);
    }

    public object[] ToArray()
    {
        var bytes = EstimateArrayBytes(Count);
        using var storage = _memoryGovernor?.ReserveTemporary(bytes, _allocationSpan);
        var result = _items.ToArray();
        storage?.Commit(bytes);
        return result;
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => this;

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    // Python list iteration follows the live list by index: replacements are
    // visible, appends are visited when reached, and removals shift or end the
    // loop like CPython. This must not bind the origin storage: growing past the
    // small-list capacity swaps the backing store mid-iteration, and a stale
    // storage enumerator would miss the new elements. It also must not use the
    // array storage version-checked enumerator, which throws a CLR exception on
    // same-length replacement instead of iterating.
    public IEnumerator<object> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private void ReplaceStorage(IEnumerable<object> values)
    {
        if (_memoryGovernor is not null)
        {
            // Fund real replacement backing before publishing it; retain the
            // old contents and charge if its construction or numeric funding fails.
            var replacement = PyListStorage.Create(values, _memoryGovernor, _allocationSpan);
            long target;
            try
            {
                target = NumericBytes((IEnumerable<object>)replacement);
                ReserveNumericGrowth(target);
            }
            catch
            {
                _memoryGovernor.Release(replacement.ReleaseCommittedBytes());
                throw;
            }
            var committedBefore = CommittedStorageBytes;
            _memoryGovernor.Release(_items.ReleaseCommittedBytes());
            _items = replacement;
            CompleteNumericChange(target, committedBefore);
            return;
        }
        _items = PyListStorage.Create(values);
    }

    private static long EstimateArrayBytes(int count) => 32L + (16L * count);

}
