using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyList : IMutablePySequenceValue, IMutablePyIndexableValue, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue
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

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public int Length => Count;

    public object this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public void Add(object value)
    {
        _items = PyListStorage.EnsureCapacity(_items, Count + 1, _memoryGovernor, _allocationSpan);
        _items.Add(value);
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
            _items = PyListStorage.EnsureCapacity(_items, checked(Count + known.Count), _memoryGovernor, _allocationSpan);
            foreach (var value in known)
            {
                _items.Add(value);
            }

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
        _items = PyListStorage.EnsureCapacity(_items, total, _memoryGovernor, _allocationSpan);
        _items.RepeatFill(originalCount, total);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor ??= governor;
        _allocationSpan ??= allocationSpan;
    }

    public void RemoveAt(int index) => _items.RemoveAt(index);

    public void RemoveRange(int index, int count)
    {
        if (count <= 0)
        {
            return;
        }

        _items.RemoveRangeAt(index, count);
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
        // only the retained backing store moves.
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
            _items.RemoveRangeAt(destination, count - destination);
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

            _items = PyListStorage.EnsureCapacity(_items, checked(Count - removeCount + staged.Count), _memoryGovernor, _allocationSpan);
            _items.ReplaceRange(bounds.Start, removeCount, staged);
            return;
        }

        if (bounds.Count != values.Count)
        {
            throw new LythonRuntimeException("ValueError", "attempt to assign sequence of size " + values.Count + " to extended slice of size " + bounds.Count, span);
        }

        var position = 0;
        foreach (var index in bounds.Indices())
        {
            _items[index] = values[position++];
        }
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

            _items = PyListStorage.Create();
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
            var released = _items.ReleaseCommittedBytes();
            if (released > 0)
            {
                _memoryGovernor.Release(released);
            }

            _items = replacement;
            return;
        }

        _items = PyListStorage.Create(values);
    }

    private static long EstimateArrayBytes(int count) => 32L + (16L * count);

}
