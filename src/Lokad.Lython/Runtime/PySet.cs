using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PySet : IEnumerable<object>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue
{
    private HashSet<object> _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private long _committedBytes;

    public PySet()
    {
        _items = new HashSet<object>(PyValueComparer.Instance);
    }

    public PySet(MemoryGovernor governor) : this(governor, null) { }

    public PySet(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this()
    {
        ArgumentNullException.ThrowIfNull(governor);
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public PySet(IEnumerable<object> items)
        : this()
    {
        foreach (var item in items)
        {
            _items.Add(item);
        }
    }

    public PySet(IEnumerable<object> items, MemoryGovernor governor) : this(items, governor, null) { }

    public PySet(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(governor, allocationSpan)
    {
        var materialized = items as object[] ?? items.ToArray();
        EnsureCapacity(materialized.Length);
        foreach (var item in materialized)
        {
            _items.Add(item);
        }
    }

    public PySet(PySet other)
    {
        _items = new HashSet<object>(other._items, PyValueComparer.Instance);
        _memoryGovernor = other._memoryGovernor;
        _allocationSpan = other._allocationSpan;
        if (_memoryGovernor is not null)
        {
            EnsureCapacity(other.Count);
        }
    }

    public PySet(PySet other, MemoryGovernor governor) : this(other, governor, null) { }

    public PySet(PySet other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(other._items, governor, allocationSpan)
    {
    }

    public int Count => _items.Count;

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public bool Add(object item)
    {
        if (_items.Contains(item))
        {
            return false;
        }

        EnsureCapacity(Count + 1);
        return _items.Add(item);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        ArgumentNullException.ThrowIfNull(governor);
        _memoryGovernor ??= governor;
        _allocationSpan ??= allocationSpan;
    }

    public bool Remove(object item) => _items.Remove(item);

    public bool TryPop(out object item)
    {
        using var enumerator = _items.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            item = PyNone.Instance;
            return false;
        }

        item = enumerator.Current;
        _items.Remove(item);
        return true;
    }

    public bool Contains(object item) => _items.Contains(item);

    public void Clear()
    {
        if (_memoryGovernor is not null)
        {
            if (_committedBytes > 0)
            {
                _memoryGovernor.Release(_committedBytes);
                _committedBytes = 0;
            }

            _items = new HashSet<object>(PyValueComparer.Instance);
            return;
        }

        _items.Clear();
    }

    public void UnionWith(PySet other)
    {
        foreach (var item in other._items)
        {
            _ = Add(item);
        }
    }

    public void IntersectWith(PySet other)
    {
        if (_memoryGovernor is null)
        {
            _items.IntersectWith(other._items);
            return;
        }

        RebuildFrom(FilterContained(other, keepContained: true));
    }

    public void ExceptWith(PySet other)
    {
        if (_memoryGovernor is null)
        {
            _items.ExceptWith(other._items);
            return;
        }

        RebuildFrom(FilterContained(other, keepContained: false));
    }

    public void SymmetricExceptWith(PySet other)
    {
        if (_memoryGovernor is null)
        {
            _items.SymmetricExceptWith(other._items);
            return;
        }

        var symmetric = new List<object>();
        foreach (var item in _items)
        {
            if (!other._items.Contains(item))
            {
                symmetric.Add(item);
            }
        }

        foreach (var item in other._items)
        {
            if (!_items.Contains(item))
            {
                symmetric.Add(item);
            }
        }

        RebuildFrom(symmetric);
    }

    public bool SetEquals(PySet other) => _items.SetEquals(other._items);

    public bool IsSubsetOf(PySet other) => _items.IsSubsetOf(other._items);

    public bool IsProperSubsetOf(PySet other) => _items.IsProperSubsetOf(other._items);

    public bool IsSupersetOf(PySet other) => _items.IsSupersetOf(other._items);

    public bool IsProperSupersetOf(PySet other) => _items.IsProperSupersetOf(other._items);

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => this;

    public PyString RenderPython(PyRenderingContext context) => PyRendering.ToReprPyString(this, context);

    public PyString RenderInterpolated(PyRenderingContext context) => PyRendering.ToReprPyString(this, context);

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureCapacity(int targetCount)
    {
        if (targetCount <= 0)
        {
            return;
        }

        var previousCapacity = _items.EnsureCapacity(0);
        if (targetCount <= previousCapacity)
        {
            return;
        }

        if (_memoryGovernor is not null)
        {
            _memoryGovernor.EnsureCanReserve(EstimateSlots(previousCapacity, targetCount), _allocationSpan);
        }

        var newCapacity = _items.EnsureCapacity(targetCount);
        if (_memoryGovernor is not null && newCapacity > previousCapacity)
        {
            var bytes = EstimateSlots(previousCapacity, newCapacity);
            _memoryGovernor.Reserve(bytes, _allocationSpan);
            _memoryGovernor.Commit(bytes);
            _committedBytes += bytes;
        }
    }

    private static long EstimateSlots(int previousCapacity, int newCapacity)
        => (24L * (newCapacity - previousCapacity)) + (previousCapacity == 0 ? 80L : 0L);

    private List<object> FilterContained(PySet other, bool keepContained)
    {
        var result = new List<object>();
        foreach (var item in _items)
        {
            if (other._items.Contains(item) == keepContained)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private void RebuildFrom(List<object> items)
    {
        _items.Clear();
        EnsureCapacity(items.Count);
        foreach (var item in items)
        {
            _items.Add(item);
        }
    }
}
