using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lokad.Lython.Runtime;

internal interface IPyDictStorage : IEnumerable<KeyValuePair<object, object>>
{
    int Count { get; }

    IEnumerable<object> Keys { get; }

    IEnumerable<object> Values { get; }

    object GetRequired(object key);

    bool TryGetValue(object key, [MaybeNullWhen(false)] out object value);

    bool ContainsKey(object key);

    bool SetItem(object key, object value);

    bool TrySetExisting(object key, object value);

    void AddNew(object key, object value);

    bool Remove(object key);

    void Clear();

    IPyDictStorage Clone();

    long ReleaseCommittedBytes();
}

internal static class PyDictStorage
{
    public const int SmallCapacity = 6;

    public static IPyDictStorage Create() => new SmallPyDictStorage();

    public static IPyDictStorage Create(int targetCount)
        => Create(targetCount, null, null);

    public static IPyDictStorage Create(int targetCount, MemoryGovernor? governor)
        => Create(targetCount, governor, null);

    public static IPyDictStorage Create(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
        => targetCount <= SmallCapacity ? new SmallPyDictStorage(governor, span) : new MapPyDictStorage(targetCount, governor, span);

    public static IPyDictStorage Create(IEnumerable<KeyValuePair<object, object>> items)
        => Create(items, null, null);

    public static IPyDictStorage Create(IEnumerable<KeyValuePair<object, object>> items, MemoryGovernor? governor)
        => Create(items, governor, null);

    public static IPyDictStorage Create(IEnumerable<KeyValuePair<object, object>> items, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var materialized = items as KeyValuePair<object, object>[] ?? items.ToArray();
        if (materialized.Length <= SmallCapacity)
        {
            var small = new SmallPyDictStorage(governor, span);
            foreach (var pair in materialized)
            {
                _ = small.SetItem(pair.Key, pair.Value);
            }

            return small;
        }

        return new MapPyDictStorage(materialized, materialized.Length, governor, span);
    }

    public static IPyDictStorage EnsureCapacity(IPyDictStorage storage, int targetCount)
        => EnsureCapacity(storage, targetCount, null, null);

    public static IPyDictStorage EnsureCapacity(IPyDictStorage storage, int targetCount, MemoryGovernor? governor)
        => EnsureCapacity(storage, targetCount, governor, null);

    public static IPyDictStorage EnsureCapacity(IPyDictStorage storage, int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
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

        var promoted = new MapPyDictStorage(small, targetCount, governor, span);
        var released = small.ReleaseCommittedBytes();
        if (released > 0)
        {
            governor?.Release(released);
        }

        return promoted;
    }
}

internal sealed class SmallPyDictStorage : IPyDictStorage
{
    private readonly List<KeyValuePair<object, object>> _items = [];
    private long _committedBytes;

    // Backing charge at the established per-slot rate: the list object plus
    // the eight-reference array it can grow without promotion.
    private const long SmallBackingBytes = 64L + (16L * 8);

    public SmallPyDictStorage()
    {
    }

    public SmallPyDictStorage(MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (governor is not null)
        {
            governor.Reserve(SmallBackingBytes, span);
            governor.Commit(SmallBackingBytes);
            _committedBytes = SmallBackingBytes;
        }
    }

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
        if (TrySetExisting(key, value))
        {
            return false;
        }

        AddNew(key, value);
        return true;
    }

    public bool TrySetExisting(object key, object value)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (PyValueComparer.Instance.Equals(_items[i].Key, key))
            {
                // Python retains the original key object when an equal key is assigned again.
                _items[i] = new KeyValuePair<object, object>(_items[i].Key, value);
                return true;
            }
        }

        return false;
    }

    public void AddNew(object key, object value)
    {
        if (_items.Count >= PyDictStorage.SmallCapacity)
        {
            throw new InvalidOperationException("SmallPyDictStorage is full.");
        }

        _items.Add(new KeyValuePair<object, object>(key, value));
    }

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value)
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

    public long ReleaseCommittedBytes()
    {
        var released = _committedBytes;
        _committedBytes = 0;
        return released;
    }

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
    {
        // Index-based enumeration permits value replacement. PyDict owns the
        // structural version check that rejects insertions and removals.
        for (var index = 0; index < _items.Count; index++)
        {
            yield return _items[index];
        }
    }

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

    public MapPyDictStorage(int capacity) : this(capacity, null, null) { }

    public MapPyDictStorage(int capacity, MemoryGovernor? governor) : this(capacity, governor, null) { }

    public MapPyDictStorage(int capacity, MemoryGovernor? governor, LythonSourceSpan? span)
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

    public MapPyDictStorage(IEnumerable<KeyValuePair<object, object>> items, int capacity) : this(items, capacity, null, null) { }

    public MapPyDictStorage(IEnumerable<KeyValuePair<object, object>> items, int capacity, MemoryGovernor? governor) : this(items, capacity, governor, null) { }

    public MapPyDictStorage(IEnumerable<KeyValuePair<object, object>> items, int capacity, MemoryGovernor? governor, LythonSourceSpan? span)
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

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value) => _items.TryGetValue(key, out value);

    public bool ContainsKey(object key) => _items.ContainsKey(key);

    public bool SetItem(object key, object value)
    {
        if (TrySetExisting(key, value))
        {
            return false;
        }

        AddNew(key, value);
        return true;
    }

    public bool TrySetExisting(object key, object value)
    {
        ref var existing = ref CollectionsMarshal.GetValueRefOrNullRef(_items, key);
        if (Unsafe.IsNullRef(ref existing))
        {
            return false;
        }

        existing = value;
        return true;
    }

    public void AddNew(object key, object value) => _items.Add(key, value);

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
