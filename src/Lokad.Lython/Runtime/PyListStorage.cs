namespace Lokad.Lython.Runtime;

internal interface IPyListStorage : IReadOnlyList<object>
{
    new object this[int index] { get; set; }

    void Add(object value);

    void AddRange(IEnumerable<object> values);

    /// <summary>Inserts a value, shifting later elements right. Room for the new
    /// count must already be ensured; implementations never allocate.</summary>
    void InsertAt(int index, object value);

    /// <summary>Removes a range in place. Shrinking never releases committed
    /// capacity accounting (the backing array is retained), matching removal
    /// through a single slot.</summary>
    void RemoveRangeAt(int index, int count);

    /// <summary>Replaces a span with new values in place. The final count
    /// must already be ensured; implementations never allocate.</summary>
    void ReplaceRange(int start, int removeCount, IReadOnlyList<object> values);

    /// <summary>Replicates the first originalCount elements until totalCount
    /// slots are filled. Capacity for the total must already be ensured.</summary>
    void RepeatFill(int originalCount, int totalCount);
    void RemoveAt(int index);

    void Clear();

    object[] ToArray();

    IPyListStorage Clone();

    long ReleaseCommittedBytes();
}

internal static class PyListStorage
{
    public const int SmallCapacity = 8;

    public static IPyListStorage Create() => new SmallPyListStorage();

    public static IPyListStorage Create(IEnumerable<object> items)
    {
        if (items is IReadOnlyCollection<object> collection)
        {
            return collection.Count <= SmallCapacity
                ? new SmallPyListStorage(items)
                : new ArrayPyListStorage(items, collection.Count, null, null);
        }

        return CreateIncrementally(items, null, null);
    }

    public static IPyListStorage Create(IEnumerable<object> items, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (items is IReadOnlyCollection<object> collection)
        {
            return collection.Count <= SmallCapacity
                ? new SmallPyListStorage(items)
                : new ArrayPyListStorage(items, collection.Count, governor, span);
        }

        return CreateIncrementally(items, governor, span);
    }

    public static IPyListStorage EnsureCapacity(IPyListStorage storage, int targetCount)
        => EnsureCapacity(storage, targetCount, null, null);

    public static IPyListStorage EnsureCapacity(IPyListStorage storage, int targetCount, MemoryGovernor? governor)
        => EnsureCapacity(storage, targetCount, governor, null);

    public static IPyListStorage EnsureCapacity(IPyListStorage storage, int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (targetCount <= SmallCapacity)
        {
            if (storage is ArrayPyListStorage arrayStorage)
            {
                arrayStorage.EnsureCapacity(targetCount, governor, span);
            }

            return storage;
        }

        if (storage is SmallPyListStorage small)
        {
            // Reserve for the requested count, not just the old contents: the caller
            // is about to append up to targetCount items, and the CLR list must not
            // grow past the ensured capacity uncharged.
            return new ArrayPyListStorage(small.ToArray(), targetCount, governor, span);
        }

        if (storage is ArrayPyListStorage array)
        {
            array.EnsureCapacity(targetCount, governor, span);
        }

        return storage;
    }

    private static IPyListStorage CreateIncrementally(
        IEnumerable<object> items,
        MemoryGovernor? governor,
        LythonSourceSpan? span)
    {
        IPyListStorage storage = new SmallPyListStorage();
        foreach (var item in items)
        {
            storage = EnsureCapacity(storage, checked(storage.Count + 1), governor, span);
            storage.Add(item);
        }

        return storage;
    }
}

internal sealed class SmallPyListStorage : IPyListStorage
{
    private readonly object[] _items;
    private int _count;

    public SmallPyListStorage()
    {
        _items = new object[PyListStorage.SmallCapacity];
    }

    public SmallPyListStorage(IEnumerable<object> items)
        : this()
    {
        foreach (var item in items)
        {
            _items[_count++] = item;
        }
    }

    public int Count => _count;

    public object this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public void Add(object value)
    {
        if (_count >= _items.Length)
        {
            throw new InvalidOperationException("SmallPyListStorage is full.");
        }

        _items[_count++] = value;
    }

    public void AddRange(IEnumerable<object> values)
    {
        foreach (var value in values)
        {
            Add(value);
        }
    }

    public void RemoveAt(int index)
    {
        Array.Copy(_items, index + 1, _items, index, _count - index - 1);
        _count--;
        Array.Clear(_items, _count, 1);
    }

    public void InsertAt(int index, object value)
    {
        Array.Copy(_items, index, _items, index + 1, _count - index);
        _items[index] = value;
        _count++;
    }

    public void RemoveRangeAt(int index, int count)
    {
        Array.Copy(_items, index + count, _items, index, _count - index - count);
        Array.Clear(_items, _count - count, count);
        _count -= count;
    }

    public void ReplaceRange(int start, int removeCount, IReadOnlyList<object> values)
    {
        var tailCount = _count - start - removeCount;
        if (values.Count != removeCount)
        {
            Array.Copy(_items, start + removeCount, _items, start + values.Count, tailCount);
        }

        for (var i = 0; i < values.Count; i++)
        {
            _items[start + i] = values[i];
        }

        var newCount = _count - removeCount + values.Count;
        if (newCount < _count)
        {
            Array.Clear(_items, newCount, _count - newCount);
        }

        _count = newCount;
    }

    public void RepeatFill(int originalCount, int totalCount)
    {
        var position = originalCount;
        while (position < totalCount)
        {
            var chunk = Math.Min(originalCount, totalCount - position);
            Array.Copy(_items, 0, _items, position, chunk);
            position += chunk;
        }

        _count = totalCount;
    }

    public void Clear()
    {
        Array.Clear(_items);
        _count = 0;
    }

    public object[] ToArray()
    {
        var copy = new object[_count];
        Array.Copy(_items, copy, _count);
        return copy;
    }

    public IPyListStorage Clone() => new SmallPyListStorage(ToArray());

    public long ReleaseCommittedBytes() => 0;

    public IEnumerator<object> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return _items[i];
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class ArrayPyListStorage : IPyListStorage
{
    private readonly List<object> _items;
    private long _committedBytes;

    public ArrayPyListStorage()
    {
        _items = [];
    }

    public ArrayPyListStorage(IEnumerable<object> items)
    {
        _items = [.. items];
    }

    public ArrayPyListStorage(IEnumerable<object> items, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var materialized = items as object[] ?? items.ToArray();
        _items = [];
        EnsureCapacity(materialized.Length, governor, span);
        _items.AddRange(materialized);
    }

    public ArrayPyListStorage(IEnumerable<object> items, int count, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        _items = [];
        EnsureCapacity(count, governor, span);
        _items.AddRange(items);
    }

    public int Count => _items.Count;

    public object this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public void Add(object value) => _items.Add(value);

    public void AddRange(IEnumerable<object> values) => _items.AddRange(values);

    public void RemoveAt(int index) => _items.RemoveAt(index);

    public void InsertAt(int index, object value) => _items.Insert(index, value);

    public void RemoveRangeAt(int index, int count) => _items.RemoveRange(index, count);

    public void ReplaceRange(int start, int removeCount, IReadOnlyList<object> values)
    {
        _items.RemoveRange(start, removeCount);
        _items.InsertRange(start, values);
    }

    public void RepeatFill(int originalCount, int totalCount)
    {
        while (_items.Count < totalCount)
        {
            var chunk = Math.Min(originalCount, totalCount - _items.Count);
            for (var i = 0; i < chunk; i++)
            {
                _items.Add(_items[i]);
            }
        }
    }

    public void Clear() => _items.Clear();

    public object[] ToArray() => [.. _items];

    public IPyListStorage Clone() => new ArrayPyListStorage([.. _items]);

    public long ReleaseCommittedBytes()
    {
        var released = _committedBytes;
        _committedBytes = 0;
        return released;
    }

    public void EnsureCapacity(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (targetCount <= _items.Capacity)
        {
            return;
        }

        var previousCapacity = _items.Capacity;
        EnsureCanReserveSlots(governor, span, previousCapacity, PredictCapacity(previousCapacity, targetCount));
        var newCapacity = _items.EnsureCapacity(targetCount);
        _committedBytes += ReserveSlots(governor, span, previousCapacity, newCapacity);
    }

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private static long ReserveSlots(MemoryGovernor? governor, LythonSourceSpan? span, int previousCapacity, int newCapacity)
    {
        if (governor is null || newCapacity <= previousCapacity)
        {
            return 0;
        }

        var bytes = 64L + (16L * (newCapacity - previousCapacity));
        governor.Reserve(bytes, span);
        governor.Commit(bytes);
        return bytes;
    }

    private static void EnsureCanReserveSlots(MemoryGovernor? governor, LythonSourceSpan? span, int previousCapacity, int newCapacity)
    {
        if (governor is null || newCapacity <= previousCapacity)
        {
            return;
        }

        governor.EnsureCanReserve(EstimateSlots(previousCapacity, newCapacity), span);
    }

    private static long EstimateSlots(int previousCapacity, int newCapacity)
        => 64L + (16L * (newCapacity - previousCapacity));

    private static int PredictCapacity(int previousCapacity, int targetCount)
    {
        if (targetCount <= previousCapacity)
        {
            return previousCapacity;
        }

        var doubled = previousCapacity == 0 ? 4L : (long)previousCapacity * 2L;
        var predicted = Math.Max(doubled, targetCount);
        return predicted > int.MaxValue ? int.MaxValue : (int)predicted;
    }
}
