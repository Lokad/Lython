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

    public PyList(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
    {
        ArgumentNullException.ThrowIfNull(governor);
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
        var materialized = values as object[] ?? values.ToArray();
        _items = PyListStorage.EnsureCapacity(_items, Count + materialized.Length, _memoryGovernor, _allocationSpan);
        _items.AddRange(materialized);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
    {
        ArgumentNullException.ThrowIfNull(governor);
        _memoryGovernor ??= governor;
        _allocationSpan ??= allocationSpan;
    }

    public void RemoveAt(int index) => _items.RemoveAt(index);

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
        ? new PyList(MaterializeSlice(indices))
        : new PyList(MaterializeSlice(indices), _memoryGovernor, _allocationSpan);

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
        => PyRendering.JoinRenderedSequence("[", new RenderedItems(this, context, interpolated: false), "]");

    public PyString RenderInterpolated(PyRenderingContext context)
        => PyRendering.JoinRenderedSequence("[", new RenderedItems(this, context, interpolated: true), "]");

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private object[] MaterializeSlice(IEnumerable<int> indices)
    {
        if (indices is ICollection<int> collection)
        {
            if (_memoryGovernor is not null)
            {
                _memoryGovernor.EnsureCanReserve(EstimateArrayBytes(collection.Count), _allocationSpan);
            }

            var result = new object[collection.Count];
            var index = 0;
            foreach (var item in indices)
            {
                result[index++] = _items[item];
            }

            return result;
        }

        var values = new List<object>();
        foreach (var item in indices)
        {
            values.Add(_items[item]);
        }

        return [.. values];
    }

    private static long EstimateArrayBytes(int count) => 32L + (16L * count);

    private sealed class RenderedItems : IEnumerable<PyString>
    {
        private readonly IEnumerable<object> _items;
        private readonly PyRenderingContext _context;
        private readonly bool _interpolated;

        public RenderedItems(IEnumerable<object> items, PyRenderingContext context, bool interpolated)
        {
            _items = items;
            _context = context;
            _interpolated = interpolated;
        }

        public IEnumerator<PyString> GetEnumerator()
        {
            foreach (var item in _items)
            {
                yield return _interpolated ? PyRendering.ToInterpolatedPyString(item, _context) : PyRendering.ToPythonPyString(item, _context);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
