namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict
{
    // The Python dictionary is the native table's owner. Live namespace views
    // instead forward _items to their existing bridge and have no native table.
    private OrderedDictionary<object, object>? _nativeItems;
    private int _nativeCapacity;
    private long _nativeCommittedBytes;
    private const long NativeShellBytes = 192;
    private const long NativeSlotBytes = 32 + 128;

    private void InitializeNativeStorage(int capacity, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        using var initial = governor?.ReserveTemporary(checked(NativeShellBytes +
            (capacity == 0 ? 0 : 96L + 2L * NativeSlotBytes * Math.Max(3, capacity))), span);
        _nativeItems = new OrderedDictionary<object, object>(capacity, PyValueComparer.Instance);
        _nativeCapacity = _nativeItems.EnsureCapacity(0);
        if (initial is not null)
        {
            var bytes = NativeShellBytes + (_nativeCapacity == 0 ? 0 : EstimateNativeSlots(0, _nativeCapacity));
            initial.Commit(bytes);
            _nativeCommittedBytes = bytes;
        }
    }

    internal static PyDict CopyStorage(IEnumerable<KeyValuePair<object, object>> entries,
        MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var capacity = entries is PyDict dictionary ? dictionary.Count
            : entries.TryGetNonEnumeratedCount(out var count) ? count : 0;
        var copy = new PyDict(capacity, governor, span);
        copy.CopyNativeEntries(entries, governor, span);
        return copy;
    }

    private static PyDict CopyStorage(IPyDictStorage source, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var copy = new PyDict(source.Count, governor, span);
        copy.CopyNativeEntries(source.Entries, governor, span);
        return copy;
    }

    private void CopyNativeEntries(IEnumerable<KeyValuePair<object, object>> entries,
        MemoryGovernor? governor, LythonSourceSpan? span)
    {
        try
        {
            foreach (var pair in entries)
            {
                PyStructuralGuard.NoteWork(null, span);
                EnsureNativeCapacity(checked(_nativeItems!.Count + 1), governor, span);
                _ = SetNativeItem(pair.Key, pair.Value);
            }
        }
        catch
        {
            var released = ((IPyDictStorage)this).ReleaseCommittedBytes();
            if (released > 0) governor!.Release(released);
            throw;
        }
    }

    private void EnsureNativeCapacity(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (targetCount <= _nativeCapacity) return;
        using var replacement = governor?.ReserveTemporary(checked(96L + 2L * NativeSlotBytes * Math.Max(3, targetCount)), span);
        var newCapacity = _nativeItems!.EnsureCapacity(targetCount);
        if (replacement is not null)
        {
            var bytes = EstimateNativeSlots(_nativeCapacity, newCapacity);
            replacement.Commit(bytes);
            _nativeCommittedBytes += bytes;
        }
        _nativeCapacity = newCapacity;
    }

    private bool SetNativeItem(object key, object value)
    {
        if (TrySetNativeExisting(key, value)) return false;
        _nativeItems!.Add(key, value);
        return true;
    }

    private bool TrySetNativeExisting(object key, object value)
    {
        var index = _nativeItems!.IndexOf(key);
        if (index < 0) return false;
        _nativeItems.SetAt(index, value);
        return true;
    }

    // Raw storage is distinct from Python iteration: protocol scans perform
    // their own mutation/reentry checks; public iterators remain version checked.
    private IEnumerable<KeyValuePair<object, object>> NativeEntries()
    {
        for (var index = 0; index < _nativeItems!.Count; index++) yield return _nativeItems.GetAt(index);
    }

    private IEnumerable<object> NativeKeys()
    {
        for (var index = 0; index < _nativeItems!.Count; index++) yield return _nativeItems.GetAt(index).Key;
    }

    private IEnumerable<object> NativeValues()
    {
        for (var index = 0; index < _nativeItems!.Count; index++) yield return _nativeItems.GetAt(index).Value;
    }

    int IPyDictStorage.Count => _nativeItems?.Count ?? _items.Count;
    IEnumerable<object> IPyDictStorage.Keys => _nativeItems is not null ? NativeKeys() : _items.Keys;
    IEnumerable<object> IPyDictStorage.Values => _nativeItems is not null ? NativeValues() : _items.Values;
    IEnumerable<KeyValuePair<object, object>> IPyDictStorage.Entries => _nativeItems is not null ? NativeEntries() : _items.Entries;
    object IPyDictStorage.GetRequired(object key) => _nativeItems is { } native ? native[key] : _items.GetRequired(key);
    bool IPyDictStorage.TryGetValue(object key, [MaybeNullWhen(false)] out object value)
        => _nativeItems is { } native ? native.TryGetValue(key, out value) : _items.TryGetValue(key, out value);
    bool IPyDictStorage.ContainsKey(object key) => _nativeItems is { } native ? native.ContainsKey(key) : _items.ContainsKey(key);
    bool IPyDictStorage.SetItem(object key, object value) => _nativeItems is not null ? SetNativeItem(key, value) : _items.SetItem(key, value);
    bool IPyDictStorage.TrySetExisting(object key, object value) => _nativeItems is not null ? TrySetNativeExisting(key, value) : _items.TrySetExisting(key, value);
    void IPyDictStorage.AddNew(object key, object value)
    {
        if (_nativeItems is { } native) native.Add(key, value);
        else _items.AddNew(key, value);
    }
    bool IPyDictStorage.Remove(object key) => _nativeItems is { } native ? native.Remove(key) : _items.Remove(key);
    void IPyDictStorage.Clear()
    {
        if (_nativeItems is { } native) native.Clear();
        else _items.Clear();
    }
    void IPyDictStorage.EnsureCapacity(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (_nativeItems is not null) EnsureNativeCapacity(targetCount, governor, span);
    }
    long IPyDictStorage.CommittedBytes => _nativeItems is not null ? _nativeCommittedBytes : _items.CommittedBytes;
    long IPyDictStorage.ReleaseCommittedBytes()
    {
        if (_nativeItems is null) return _items.ReleaseCommittedBytes();
        var released = _nativeCommittedBytes;
        _nativeCommittedBytes = 0;
        _nativeCapacity = 0;
        return released;
    }

    private static long EstimateNativeSlots(int previousCapacity, int newCapacity)
        => NativeSlotBytes * (newCapacity - previousCapacity) + (previousCapacity == 0 ? 96L : 0L);
}
