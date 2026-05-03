using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue
{
    private IPyDictStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;

    public PyDict()
    {
        _items = PyDictStorage.Create();
    }

    public PyDict(MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
    {
        ArgumentNullException.ThrowIfNull(governor);
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyDictStorage.Create();
    }

    public PyDict(PyDict other)
    {
        _memoryGovernor = other._memoryGovernor;
        _allocationSpan = other._allocationSpan;
        _items = other._memoryGovernor is null
            ? other._items.Clone()
            : PyDictStorage.Create(other._items, other._memoryGovernor, other._allocationSpan);
    }

    public PyDict(PyDict other, MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
    {
        ArgumentNullException.ThrowIfNull(governor);
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyDictStorage.Create(other._items, governor, allocationSpan);
    }

    public int Count => _items.Count;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public IEnumerable<object> Keys
    {
        get
        {
            foreach (var key in _items.Keys)
            {
                yield return FromStorageKey(key);
            }
        }
    }

    public IEnumerable<object> Values
    {
        get
        {
            foreach (var value in _items.Values)
            {
                yield return FromStorageValue(value);
            }
        }
    }

    public IEnumerable<KeyValuePair<object, object>> Items => this;

    public object GetItem(object key) => FromStorageValue(_items.GetRequired(ToStorageKey(key)));

    public bool TryGetValue(object key, out object value)
    {
        if (_items.TryGetValue(ToStorageKey(key), out var stored))
        {
            value = FromStorageValue(stored);
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    public bool ContainsKey(object key) => _items.ContainsKey(ToStorageKey(key));

    public void SetItem(object key, object value)
    {
        var storageKey = ToStorageKey(key);
        if (!_items.ContainsKey(storageKey))
        {
            _items = PyDictStorage.EnsureCapacity(_items, Count + 1, _memoryGovernor, _allocationSpan);
        }

        _ = _items.SetItem(storageKey, ToStorageValue(value));
    }

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan = null)
    {
        ArgumentNullException.ThrowIfNull(governor);
        _memoryGovernor ??= governor;
        _allocationSpan ??= allocationSpan;
    }

    public bool Remove(object key) => _items.Remove(ToStorageKey(key));

    public void Clear()
    {
        if (_memoryGovernor is not null)
        {
            var released = _items.ReleaseCommittedBytes();
            if (released > 0)
            {
                _memoryGovernor.Release(released);
            }

            _items = PyDictStorage.Create();
            return;
        }

        _items.Clear();
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => Keys;

    public PyString RenderPython(PyRenderingContext context) => PyRendering.JoinRenderedDictionary(this, context, interpolated: false);

    public PyString RenderInterpolated(PyRenderingContext context) => PyRendering.JoinRenderedDictionary(this, context, interpolated: true);

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
    {
        foreach (var pair in _items)
        {
            yield return new KeyValuePair<object, object>(FromStorageKey(pair.Key), FromStorageValue(pair.Value));
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private static object ToStorageKey(object key) => ReferenceEquals(key, PyNone.Instance) ? PyNone.Instance : key;

    private static object FromStorageKey(object key) => key;

    private static object ToStorageValue(object value) => value;

    private static object FromStorageValue(object value) => value;
}
