using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyList : IMutablePySequenceValue, IMutablePyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPyOwnershipSnapshot
{
    private IPyListStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;


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
        var charge = _items.ReleaseCommittedBytes();
        _items = null!;
        _memoryGovernor!.Release(charge);
    }

    public PyList(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyListStorage.Create(items, governor, allocationSpan);
    }

    public PyList(PyList other)
    {
        _memoryGovernor = other._memoryGovernor;
        _allocationSpan = other._allocationSpan;
        _items = other._memoryGovernor is null
            ? other._items.Clone()
            : PyListStorage.Create(other._items, other._memoryGovernor, other._allocationSpan);
    }

    public int Count => _items.Count;

    // Current backing funding for unpublished rollback and explicit replacement.
    // Capacity includes numeric-box headroom independent of its current contents.
    internal long CommittedStorageBytes => _items.CommittedBytes;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public int Length => Count;

    public object this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public void Add(object value)
    {
        _items = PyListStorage.EnsureCapacity(_items, checked(Count + 1), _memoryGovernor, _allocationSpan);
        _items.Add(value);
    }

    public void AddRange(IEnumerable<object> values) => AddRange(values, null, null);

    internal void AddRange(IEnumerable<object> values, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        context?.CheckExecution(span);
        if (values is PyIteration.CheckedSequence { Source: PyList or IPyListStorage or IReadOnlyCollection<object> } checkedSequence)
        {
            values = checkedSequence.Source;
        }

        if (values is PyList sourceList)
        {
            // Snapshot the source (which may be this list) so extending appends
            // the original elements; the copy itself is budget-checked.
            AddRange(sourceList.ToArray(), context, span);
            return;
        }

        if (values is IPyListStorage sourceStorage)
        {
            AddRange(sourceStorage.ToArray(), context, span);
            return;
        }

        // List-backed inputs are snapshotted above (PyList implements this interface
        // through its sequence interfaces, so it must never reach this live count).
        if (values is IReadOnlyCollection<object> known)
        {
            // Admit the reported count before mutation. A changing collection
            // can yield more, so each store also ensures its actual capacity.
            _items = PyListStorage.EnsureCapacity(_items, checked(Count + known.Count), _memoryGovernor, _allocationSpan);

            foreach (var value in known)
            {
                context?.CheckExecution(span);
                Add(value);
            }

            context?.CheckExecution(span);
            return;
        }

        try
        {
            // Unknown-length inputs stream incrementally, admitting actual
            // capacity before each store. Failing iterables retain their
            // successful prefix and its funding.
            foreach (var value in values)
            {
                context?.CheckExecution(span);
                Add(value);
            }
            context?.CheckExecution(span);
        }
        catch (InvalidOperationException)
        {
            // Iteration observed our own concurrent growth (for example extending
            // with a live iterator over this list). That cannot terminate like
            // CPython does, so fail explicitly instead of leaking the CLR error.
            throw RuntimeErrors.Runtime("list modified during extension.", span ?? _allocationSpan);
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
            context.CheckExecution(span);
            await foreach (var value in LythonRuntime.ToSequenceAsync(source, span, context).ConfigureAwait(false))
            {
                context.CheckExecution(span);
                Add(value);
            }
            context.CheckExecution(span);
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
        _items = PyListStorage.EnsureCapacity(_items, checked(Count + 1), _memoryGovernor, _allocationSpan);
        _items.InsertAt(normalized, value);
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

    // The original backing remains funded until restoration. Callbacks observe
    // an empty list; its zero capacity also detects append/pop mutations without
    // adding mutation counters to every ordinary list operation.
    internal IPyListStorage DetachForSort()
    {
        var original = _items;
        _items = new ArrayPyListStorage(0, _memoryGovernor, _allocationSpan);
        return original;
    }

    internal bool RestoreAfterSort(IPyListStorage original, PyStableSort.Buffer? sorted)
    {
        var modified = _items is not ArrayPyListStorage { Capacity: 0 };
        // This allocation-free cleanup completes even after cancellation. It
        // restores the complete permutation before releasing callback storage.
        if (sorted is not null)
            for (var i = 0; i < original.Count; i++) original[i] = sorted[i].Value;
        var discarded = _items;
        _items = original;
        _memoryGovernor?.Release(discarded.ReleaseCommittedBytes());
        return modified;
    }

    public void RepeatInPlace(int count, LythonSourceSpan span)
    {
        if (_items is ArrayPyListStorage { Count: 0, Capacity: 0 }) return;
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
        _items = PyListStorage.EnsureCapacity(_items, total, _memoryGovernor, _allocationSpan);
        _items.RepeatFill(originalCount, total);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (_memoryGovernor is null)
        {
            // Prepaid producer storage retains its original funding. Admission
            // here conservatively funds this backing before governed mutation.
            _items.AttachMemoryGovernor(governor, allocationSpan);
            _memoryGovernor = governor;
        }
        _allocationSpan ??= allocationSpan;
    }

    public void RemoveAt(int index) => _items.RemoveAt(index);

    public void RemoveRange(int index, int count)
    {
        if (count <= 0) return;
        _items.RemoveRangeAt(index, count);
    }

    public void DeleteSlice(PyIndexing.SliceBounds bounds)
    {
        if (bounds.Step == 1)
        {
            RemoveRange(bounds.Start, Math.Max(bounds.End - bounds.Start, 0));
            return;
        }
        if (bounds.Count == 0) return;
        var count = Count;
        var destination = 0;
        for (var source = 0; source < count; source++)
        {
            if (!bounds.Contains(source))
            {
                if (destination != source) _items[destination] = _items[source];
                destination++;
            }
        }
        if (destination < count) _items.RemoveRangeAt(destination, count - destination);
    }

    public void SetSlice(PyIndexing.SliceBounds bounds, IReadOnlyList<object> values, LythonSourceSpan span)
    {
        if (bounds.Step == 1)
        {
            var removeCount = Math.Max(bounds.End - bounds.Start, 0);
            IReadOnlyList<object> staged = values;
            if (values is PyList sourceList) staged = sourceList.ToArray();
            else if (values is IPyListStorage sourceStorage) staged = sourceStorage.ToArray();
            _items = PyListStorage.EnsureCapacity(_items, checked(Count - removeCount + staged.Count), _memoryGovernor, _allocationSpan);
            _items.ReplaceRange(bounds.Start, removeCount, staged);
            return;
        }
        if (bounds.Count != values.Count)
            throw new LythonRuntimeException("ValueError", "attempt to assign sequence of size " + values.Count + " to extended slice of size " + bounds.Count, span);
        var position = 0;
        foreach (var index in bounds.Indices()) _items[index] = values[position++];
    }

    public void Clear()
    {
        if (_items is ArrayPyListStorage { Count: 0, Capacity: 0 }) return;
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

            return;
        }

        _items.Clear();
    }

    public object GetItem(int index) => _items[index];

    public object CreateSlice(IEnumerable<object> items) => _memoryGovernor is null
        ? new PyList(items)
        : new PyList(items, _memoryGovernor, _allocationSpan);

    public void SetItem(int index, object value) => _items[index] = value;

    public object GetIndex(int index) => _items[index];

    public object GetSlice(IEnumerable<int> indices) => _memoryGovernor is null
        ? new PyList(PySequenceMaterialization.MaterializeSlice(_items, indices, null, _allocationSpan))
        : new PyList(PySequenceMaterialization.MaterializeSlice(_items, indices, _memoryGovernor, _allocationSpan), _memoryGovernor, _allocationSpan);

    public void SetIndex(int index, object value) => _items[index] = value;

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
            // Admit replacement while existing backing and its aliases stay funded.
            var replacement = PyListStorage.Create(values, _memoryGovernor, _allocationSpan);
            _memoryGovernor.Release(_items.ReleaseCommittedBytes());
            _items = replacement;
            return;
        }
        _items = PyListStorage.Create(values);
    }

    private static long EstimateArrayBytes(int count) => 32L + (16L * count);

}
