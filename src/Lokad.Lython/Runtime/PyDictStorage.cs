namespace Lokad.Lython.Runtime;

internal interface IPyDictStorage : IEnumerable<KeyValuePair<object, object>>
{
    int Count { get; }

    IEnumerable<object> Keys { get; }

    IEnumerable<object> Values { get; }

    object GetRequired(object key);

    bool TryGetValue(object key, out object value);

    bool ContainsKey(object key);

    bool SetItem(object key, object value);

    bool Remove(object key);

    void Clear();

    IPyDictStorage Clone();

    long ReleaseCommittedBytes();
}

internal static class PyDictStorage
{
    public const int SmallCapacity = 6;

    public static IPyDictStorage Create() => new SmallPyDictStorage();

    public static IPyDictStorage Create(int targetCount, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
        => targetCount <= SmallCapacity ? new SmallPyDictStorage() : new MapPyDictStorage(targetCount, governor, span);

    public static IPyDictStorage Create(IEnumerable<KeyValuePair<object, object>> items, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
    {
        var materialized = items as KeyValuePair<object, object>[] ?? items.ToArray();
        if (materialized.Length <= SmallCapacity)
        {
            var small = new SmallPyDictStorage();
            foreach (var pair in materialized)
            {
                _ = small.SetItem(pair.Key, pair.Value);
            }

            return small;
        }

        return new MapPyDictStorage(materialized, materialized.Length, governor, span);
    }

    public static IPyDictStorage EnsureCapacity(IPyDictStorage storage, int targetCount, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
    {
        if (storage is MapPyDictStorage map)
        {
            map.EnsureCapacity(targetCount, governor, span);
            return storage;
        }

        if (targetCount <= SmallCapacity || storage is not SmallPyDictStorage small)
        {
            return storage;
        }

        return new MapPyDictStorage(small, targetCount, governor, span);
    }
}

internal sealed class SmallPyDictStorage : IPyDictStorage
{
    private readonly List<KeyValuePair<object, object>> _items = [];

    public int Count => _items.Count;

    public IEnumerable<object> Keys => _items.Select(pair => pair.Key);

    public IEnumerable<object> Values => _items.Select(pair => pair.Value);

    public object GetRequired(object key)
    {
        if (!TryGetValue(key, out var value))
        {
            throw new KeyNotFoundException();
        }

        return value;
    }

    public bool SetItem(object key, object value)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (PyValueComparer.Instance.Equals(_items[i].Key, key))
            {
                _items[i] = new KeyValuePair<object, object>(key, value);
                return false;
            }
        }

        if (_items.Count >= PyDictStorage.SmallCapacity)
        {
            throw new InvalidOperationException("SmallPyDictStorage is full.");
        }

        _items.Add(new KeyValuePair<object, object>(key, value));
        return true;
    }

    public bool TryGetValue(object key, out object value)
    {
        foreach (var pair in _items)
        {
            if (PyValueComparer.Instance.Equals(pair.Key, key))
            {
                value = pair.Value;
                return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    public bool ContainsKey(object key)
    {
        foreach (var pair in _items)
        {
            if (PyValueComparer.Instance.Equals(pair.Key, key))
            {
                return true;
            }
        }

        return false;
    }

    public bool Remove(object key)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (PyValueComparer.Instance.Equals(_items[i].Key, key))
            {
                _items.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public void Clear() => _items.Clear();

    public IPyDictStorage Clone()
    {
        var clone = new SmallPyDictStorage();
        foreach (var pair in _items)
        {
            clone._items.Add(pair);
        }

        return clone;
    }

    public long ReleaseCommittedBytes() => 0;

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class MapPyDictStorage : IPyDictStorage
{
    private readonly Dictionary<object, object> _items;
    private int _capacity;
    private long _committedBytes;

    public MapPyDictStorage()
    {
        _items = new Dictionary<object, object>(PyValueComparer.Instance);
    }

    public MapPyDictStorage(int capacity, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
    {
        _items = new Dictionary<object, object>(PyValueComparer.Instance);
        EnsureCapacity(capacity, governor, span);
    }

    public MapPyDictStorage(IEnumerable<KeyValuePair<object, object>> items)
        : this()
    {
        foreach (var pair in items)
        {
            _items[pair.Key] = pair.Value;
        }
    }

    public MapPyDictStorage(IEnumerable<KeyValuePair<object, object>> items, int capacity, MemoryGovernor? governor = null, LythonSourceSpan? span = null)
        : this(capacity, governor, span)
    {
        foreach (var pair in items)
        {
            _items[pair.Key] = pair.Value;
        }
    }

    public int Count => _items.Count;

    public IEnumerable<object> Keys => _items.Keys;

    public IEnumerable<object> Values => _items.Values;

    public object GetRequired(object key) => _items[key];

    public bool TryGetValue(object key, out object value) => _items.TryGetValue(key, out value!);

    public bool ContainsKey(object key) => _items.ContainsKey(key);

    public bool SetItem(object key, object value)
    {
        var adding = !_items.ContainsKey(key);
        _items[key] = value;
        return adding;
    }

    public bool Remove(object key) => _items.Remove(key);

    public void Clear() => _items.Clear();

    public IPyDictStorage Clone() => new MapPyDictStorage(_items, _items.Count);

    public long ReleaseCommittedBytes()
    {
        var released = _committedBytes;
        _committedBytes = 0;
        _capacity = 0;
        return released;
    }

    public void EnsureCapacity(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (targetCount <= _capacity)
        {
            return;
        }

        EnsureCanReserveSlots(governor, span, _capacity, targetCount);
        var newCapacity = _items.EnsureCapacity(targetCount);
        _committedBytes += ReserveSlots(governor, span, _capacity, newCapacity);
        _capacity = newCapacity;
    }

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private static long ReserveSlots(MemoryGovernor? governor, LythonSourceSpan? span, int previousCapacity, int newCapacity)
    {
        if (governor is null || newCapacity <= previousCapacity)
        {
            return 0;
        }

        var bytes = (32L * (newCapacity - previousCapacity)) + (previousCapacity == 0 ? 96L : 0L);
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
        return bytes;
    }

    private static void EnsureCanReserveSlots(MemoryGovernor? governor, LythonSourceSpan? span, int previousCapacity, int targetCount)
    {
        if (governor is null || targetCount <= previousCapacity)
        {
            return;
        }

        governor.EnsureCanReserve(EstimateSlots(previousCapacity, targetCount), span);
    }

    private static long EstimateSlots(int previousCapacity, int newCapacity)
        => (32L * (newCapacity - previousCapacity)) + (previousCapacity == 0 ? 96L : 0L);
}
