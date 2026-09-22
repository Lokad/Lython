using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyList : IMutablePySequenceValue, IMutablePyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    private IPyListStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private AdoptedScalarCoupons? _scalarCoupons;

    public PyList()
    {
        _items = PyListStorage.Create();
    }

    public PyList(IEnumerable<object> items)
    {
        _items = PyListStorage.Create(items);
    }

    public PyList(IEnumerable<object> items, MemoryGovernor governor) : this(items, governor, null) { }

    public PyList(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyListStorage.Create(items, governor, allocationSpan);
        AdoptInitialItems();
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
            AdoptInitialItems();
        }
    }

    public int Count => _items.Count;

    // Current committed backing charges, for pooled owners that release them
    // if this list is dropped without wholesale storage replacement. Adopted
    // scalar coupons fold in, so snapshots and drop sweeps carry them.
    internal long CommittedStorageBytes => _items.CommittedBytes + (_scalarCoupons?.CommittedBytes ?? 0);

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
        AdoptIncoming(value);
        try
        {
            _items = PyListStorage.EnsureCapacity(_items, Count + 1, _memoryGovernor, _allocationSpan);
        }
        catch (Exception)
        {
            UnadoptIncoming(value);
            throw;
        }

        _items.Add(value);
        NoteGrowth(committedBefore);
    }

    // Keeps a tracked coupon current across growth: capacity commits below change
    // the backing this entry owns, so refresh the snapshot after successful growth.
    // Denied growth throws before mutating, leaving the old coupon (and retry
    // headroom) intact. Untracked values cost one lookup and no entry. Totals
    // include adopted coupons, so coupon commits refresh the snapshot as well.
    private void NoteGrowth(long committedBefore)
    {
        if (CommittedStorageBytes != committedBefore)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }
    }

    // Adopts construction contents with a refund of the orphaned storage when
    // a coupon denies: callers publish nothing on failure.
    private void AdoptInitialItems()
    {
        var incoming = new AdoptedScalarCoupons();
        try
        {
            incoming.AdoptAll(_items, _memoryGovernor!, _allocationSpan);
        }
        catch
        {
            var orphaned = _items.ReleaseCommittedBytes();
            if (orphaned > 0)
            {
                _memoryGovernor!.Release(orphaned);
            }

            throw;
        }

        _scalarCoupons = incoming.CommittedBytes > 0 ? incoming : null;
    }

    // Adopts one incoming value when governed; denies before the caller mutates.
    private void AdoptIncoming(object value)
    {
        if (_memoryGovernor is null || !AdoptedScalarCoupons.IsAdoptableScalar(value))
        {
            return;
        }

        _scalarCoupons ??= new AdoptedScalarCoupons();
        _scalarCoupons.Adopt(value, _memoryGovernor, _allocationSpan);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    private void AdoptAllIncoming(IEnumerable<object> values)
    {
        if (_memoryGovernor is null)
        {
            return;
        }

        var coupons = _scalarCoupons ?? new AdoptedScalarCoupons();
        coupons.AdoptAll(values, _memoryGovernor, _allocationSpan);
        _scalarCoupons = coupons.CommittedBytes > 0 ? coupons : null;
    }

    private void UnadoptIncoming(object value)
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            return;
        }

        _scalarCoupons.Release(value, _memoryGovernor);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    private void UnadoptAllIncoming(IEnumerable<object> values)
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            return;
        }

        foreach (var value in values)
        {
            _scalarCoupons.Release(value, _memoryGovernor);
        }

        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    private void ReleaseOutgoing(object? value)
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            return;
        }

        _scalarCoupons.Release(value, _memoryGovernor);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    private void ReleaseAllOutgoing(object[] values)
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            return;
        }

        foreach (var value in values)
        {
            _scalarCoupons.Release(value, _memoryGovernor);
        }

        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    private void ReleaseAllCoupons()
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            _scalarCoupons = null;
            return;
        }

        _scalarCoupons.ReleaseAll(_memoryGovernor);
        _scalarCoupons = null;
    }

    public void AddRange(IEnumerable<object> values)
    {
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
            AdoptAllIncoming(known);
            try
            {
                _items = PyListStorage.EnsureCapacity(_items, checked(Count + known.Count), _memoryGovernor, _allocationSpan);
            }
            catch (Exception)
            {
                UnadoptAllIncoming(known);
                throw;
            }

            foreach (var value in known)
            {
                _items.Add(value);
            }

            NoteGrowth(committedBefore);
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
                    context.CheckExecutionBudget(span);
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
        AdoptIncoming(value);
        try
        {
            _items = PyListStorage.EnsureCapacity(_items, checked(Count + 1), _memoryGovernor, _allocationSpan);
        }
        catch (Exception)
        {
            UnadoptIncoming(value);
            throw;
        }

        _items.InsertAt(normalized, value);
        NoteGrowth(committedBefore);
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
        {
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        }

        var total = (int)totalLength;
        var originalCount = Count;
        var committedBefore = CommittedStorageBytes;
        _items = PyListStorage.EnsureCapacity(_items, total, _memoryGovernor, _allocationSpan);
        _items.RepeatFill(originalCount, total);
        if (_scalarCoupons is not null)
        {
            for (var i = 0; i < originalCount; i++)
            {
                _scalarCoupons.AddRef(_items[i], count - 1);
            }
        }

        NoteGrowth(committedBefore);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor ??= governor;
        _allocationSpan ??= allocationSpan;
    }

    public void RemoveAt(int index)
    {
        // The coupon release below shrinks the owned total, so refresh the
        // pool snapshot or a later drop sweep would over-release.
        var committedBefore = CommittedStorageBytes;
        var outgoing = _items[index];
        _items.RemoveAt(index);
        ReleaseOutgoing(outgoing);
        NoteGrowth(committedBefore);
    }

    public void RemoveRange(int index, int count)
    {
        if (count <= 0)
        {
            return;
        }

        // Index reads throw before mutating on a bad range, matching the
        // throwing read the old single-slot path performed.
        var committedBefore = CommittedStorageBytes;
        var outgoing = new object[count];
        for (var i = 0; i < count; i++)
        {
            outgoing[i] = _items[index + i];
        }

        _items.RemoveRangeAt(index, count);
        ReleaseAllOutgoing(outgoing);
        NoteGrowth(committedBefore);
    }

    public void DeleteSlice(PyIndexing.SliceBounds bounds)
    {
        if (bounds.Step == 1)
        {
            RemoveRange(bounds.Start, Math.Max(bounds.End - bounds.Start, 0));
            return;
        }

        var removeCount = bounds.Count;
        if (removeCount == 0)
        {
            return;
        }

        // Compact survivors forward, then drop the tail: no scratch arrays,
        // only the retained backing store moves. The compaction permutes held
        // references, so adoption refcounts are untouched by it.
        var count = Count;
        var destination = 0;
        for (var source = 0; source < count; source++)
        {
            if (!bounds.Contains(source))
            {
                if (destination != source)
                {
                    _items[destination] = _items[source];
                }

                destination++;
            }
        }

        if (destination < count)
        {
            RemoveRange(destination, count - destination);
        }
    }

    public void SetSlice(PyIndexing.SliceBounds bounds, IReadOnlyList<object> values, LythonSourceSpan span)
    {
        if (bounds.Step == 1)
        {
            var removeCount = Math.Max(bounds.End - bounds.Start, 0);
            IReadOnlyList<object> staged = values;
            if (values is PyList sourceList)
            {
                // The right-hand side would observe its own replacement;
                // snapshot it first, as if it had been evaluated eagerly.
                staged = sourceList.ToArray();
            }
            else if (values is IPyListStorage sourceStorage)
            {
                staged = sourceStorage.ToArray();
            }

            var committedBefore = CommittedStorageBytes;
            var outgoing = new object[removeCount];
            for (var i = 0; i < removeCount; i++)
            {
                outgoing[i] = _items[bounds.Start + i];
            }

            AdoptAllIncoming(staged);
            try
            {
                _items = PyListStorage.EnsureCapacity(_items, checked(Count - removeCount + staged.Count), _memoryGovernor, _allocationSpan);
            }
            catch (Exception)
            {
                UnadoptAllIncoming(staged);
                throw;
            }

            _items.ReplaceRange(bounds.Start, removeCount, staged);
            ReleaseAllOutgoing(outgoing);
            NoteGrowth(committedBefore);
            return;
        }

        if (bounds.Count != values.Count)
        {
            throw new LythonRuntimeException("ValueError", "attempt to assign sequence of size " + values.Count + " to extended slice of size " + bounds.Count, span);
        }

        var indices = new int[bounds.Count];
        var fill = 0;
        foreach (var index in bounds.Indices())
        {
            indices[fill++] = index;
        }

        var replaced = new object[bounds.Count];
        for (var i = 0; i < indices.Length; i++)
        {
            replaced[i] = _items[indices[i]];
        }

        var extendedBefore = CommittedStorageBytes;
        AdoptAllIncoming(values);
        var position = 0;
        for (var i = 0; i < indices.Length; i++)
        {
            _items[indices[i]] = values[position++];
        }

        ReleaseAllOutgoing(replaced);
        NoteGrowth(extendedBefore);
    }

    public void Clear()
    {
        if (_memoryGovernor is not null)
        {
            var released = _items.ReleaseCommittedBytes();
            if (released > 0)
            {
                _memoryGovernor.Release(released);
            }

            _items = PyListStorage.Create(System.Array.Empty<object>(), _memoryGovernor, _allocationSpan);
            ReleaseAllCoupons();
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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

    // Replaces one slot: the incoming coupon commits before the store (a denial
    // leaves the old element in place), the store itself cannot fail, and the
    // outgoing reference releases after. Same-reference stores skip both sides.
    private void SetElement(int index, object value)
    {
        var outgoing = _items[index];
        if (ReferenceEquals(outgoing, value))
        {
            return;
        }

        var committedBefore = CommittedStorageBytes;
        AdoptIncoming(value);
        _items[index] = value;
        ReleaseOutgoing(outgoing);
        NoteGrowth(committedBefore);
    }

    public object[] ToArray()
    {
        if (_memoryGovernor is not null)
        {
            _memoryGovernor.EnsureCanReserve(EstimateArrayBytes(Count), _allocationSpan);
        }

        return [.. _items];
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => this;

    public PyString RenderPython(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.ToReprPyString(this, context);

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private void ReplaceStorage(IEnumerable<object> values)
    {
        if (_memoryGovernor is not null)
        {
            // Keep the old storage charged until its replacement succeeds so a
            // failed allocation leaves both the list and its accounting intact.
            var replacement = PyListStorage.Create(values, _memoryGovernor, _allocationSpan);
            var incoming = new AdoptedScalarCoupons();
            try
            {
                incoming.AdoptAll(replacement, _memoryGovernor, _allocationSpan);
            }
            catch
            {
                var orphaned = replacement.ReleaseCommittedBytes();
                if (orphaned > 0)
                {
                    _memoryGovernor.Release(orphaned);
                }

                throw;
            }

            var released = _items.ReleaseCommittedBytes();
            if (released > 0)
            {
                _memoryGovernor.Release(released);
            }

            ReleaseAllCoupons();
            _items = replacement;
            _scalarCoupons = incoming.CommittedBytes > 0 ? incoming : null;
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
            return;
        }

        _items = PyListStorage.Create(values);
    }

    private static long EstimateArrayBytes(int count) => 32L + (16L * count);

}
