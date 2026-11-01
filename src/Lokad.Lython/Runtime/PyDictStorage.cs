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

    long CommittedBytes { get; }

    long ReleaseCommittedBytes();

}

internal static class PyDictStorage
{
    public static IPyDictStorage Create() => Create(0, null, null);

    public static IPyDictStorage Create(int targetCount) => Create(targetCount, null, null);

    public static IPyDictStorage Create(int targetCount, MemoryGovernor? governor)
        => Create(targetCount, governor, null);

    public static IPyDictStorage Create(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
        => new OrderedPyDictStorage(targetCount, governor, span);

    public static IPyDictStorage Create(IEnumerable<KeyValuePair<object, object>> items)
        => Create(items, null, null);

    public static IPyDictStorage Create(IEnumerable<KeyValuePair<object, object>> items, MemoryGovernor? governor)
        => Create(items, governor, null);

    public static IPyDictStorage Create(IEnumerable<KeyValuePair<object, object>> items, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var materialized = items as KeyValuePair<object, object>[] ?? items.ToArray();
        return new OrderedPyDictStorage(materialized, materialized.Length, governor, span);
    }

    public static IPyDictStorage EnsureCapacity(IPyDictStorage storage, int targetCount)
        => EnsureCapacity(storage, targetCount, null, null);

    public static IPyDictStorage EnsureCapacity(IPyDictStorage storage, int targetCount, MemoryGovernor? governor)
        => EnsureCapacity(storage, targetCount, governor, null);

    public static IPyDictStorage EnsureCapacity(IPyDictStorage storage, int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (storage is OrderedPyDictStorage ordered) ordered.EnsureCapacity(targetCount, governor, span);
        return storage;
    }
}

internal sealed class OrderedPyDictStorage : IPyDictStorage
{
    private readonly OrderedDictionary<object, object> _items;
    private int _capacity;
    private long _committedBytes;
    private const long ShellBytes = 192;

    public OrderedPyDictStorage(int capacity, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        // Cover the shell and initial table before either allocation. Capacity
        // rounding uses the same conservative headroom as subsequent growth.
        using var initial = governor?.ReserveTemporary(checked(ShellBytes +
            (capacity == 0 ? 0 : 96L + 128L * Math.Max(3, capacity))), span);
        _items = new OrderedDictionary<object, object>(capacity, PyValueComparer.Instance);
        _capacity = _items.EnsureCapacity(0);
        if (initial is not null)
        {
            var bytes = ShellBytes + (_capacity == 0 ? 0 : EstimateSlots(0, _capacity));
            initial.Commit(bytes);
            _committedBytes = bytes;
        }
    }

    public OrderedPyDictStorage(IEnumerable<KeyValuePair<object, object>> items, int capacity,
        MemoryGovernor? governor, LythonSourceSpan? span) : this(capacity, governor, span)
    {
        try
        {
            foreach (var pair in items) _ = SetItem(pair.Key, pair.Value);
        }
        catch
        {
            var released = ReleaseCommittedBytes();
            if (released > 0) governor!.Release(released);
            throw;
        }
    }

    public int Count => _items.Count;

    // PyDict checks structural mutation. Value replacement must remain visible
    // without inheriting the BCL enumerator's value-version rules.
    public IEnumerable<object> Keys
    {
        get
        {
            for (var index = 0; index < _items.Count; index++) yield return _items.GetAt(index).Key;
        }
    }

    public IEnumerable<object> Values
    {
        get
        {
            for (var index = 0; index < _items.Count; index++) yield return _items.GetAt(index).Value;
        }
    }

    public object GetRequired(object key) => _items[key];

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value) => _items.TryGetValue(key, out value);

    public bool ContainsKey(object key) => _items.ContainsKey(key);

    public bool SetItem(object key, object value)
    {
        if (TrySetExisting(key, value)) return false;
        AddNew(key, value);
        return true;
    }

    public bool TrySetExisting(object key, object value)
    {
        var index = _items.IndexOf(key);
        if (index < 0) return false;
        // Updating the value by index retains the original equal key object.
        _items.SetAt(index, value);
        return true;
    }

    public void AddNew(object key, object value) => _items.Add(key, value);

    public bool Remove(object key) => _items.Remove(key);

    public void Clear() => _items.Clear();

    public IPyDictStorage Clone() => new OrderedPyDictStorage(_items, _items.Count, null, null);

    public long CommittedBytes => _committedBytes;

    public long ReleaseCommittedBytes()
    {
        var released = _committedBytes;
        _committedBytes = 0;
        _capacity = 0;
        return released;
    }

    public void EnsureCapacity(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (targetCount <= _capacity) return;
        // The old table remains funded throughout replacement. No table alias
        // escapes: views/iterators retain this owner and read its current slots.
        using var replacement = governor?.ReserveTemporary(checked(96L + 128L * Math.Max(3, targetCount)), span);
        var newCapacity = _items.EnsureCapacity(targetCount);
        if (replacement is not null)
        {
            var bytes = EstimateSlots(_capacity, newCapacity);
            replacement.Commit(bytes);
            _committedBytes += bytes;
        }
        _capacity = newCapacity;
    }

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
    {
        for (var index = 0; index < _items.Count; index++) yield return _items.GetAt(index);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private static long EstimateSlots(int previousCapacity, int newCapacity)
        => 32L * (newCapacity - previousCapacity) + (previousCapacity == 0 ? 96L : 0L);
}
