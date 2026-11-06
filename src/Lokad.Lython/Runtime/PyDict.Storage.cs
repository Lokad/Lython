using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict
{
    // Ordered payloads and bucket heads share one array. Payload writes must
    // preserve the bucket head belonging to the destination's physical slot.
    private struct NativeEntry
    {
        internal object? Key, Value;
        internal long Sequence;
        internal uint Hash;
        internal int Next, Bucket;
        internal bool Protocol;
    }

    // Null identifies a live namespace bridge, not an empty native table.
    private NativeEntry[]? _nativeItems;
    private int _nativeCount, _nativeProtocolCount;
    private long _nativeSequence;
    private long _nativeCommittedBytes;
    private const long NativeShellBytes = 192;
    private const long NativeEntryBytes = 40;
    private long NativeSlotBytes => NativeEntryBytes + (_keysOnly ? 64 : 128);

    private static int NativeCapacity(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > Array.MaxLength) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0) return 0;
        var rounded = BitOperations.RoundUpToPowerOf2((uint)Math.Max(2, count));
        return rounded == 0 || rounded > Array.MaxLength ? Array.MaxLength : (int)rounded;
    }

    private static uint NativeHash(object key)
    {
        // Private mixing preserves public hashes and spreads integer key bits.
        return MixNativeHash(PyValueComparer.Instance.GetHashCode(key));
    }

    private static uint MixNativeHash(int normalized)
    {
        var hash = unchecked((uint)normalized);
        hash ^= hash >> 16;
        hash = unchecked(hash * 0x9e3779b9u);
        return hash ^ (hash >> 16);
    }

    private static int NativeBucket(uint hash, int capacity)
        => (int)((capacity & (capacity - 1)) == 0 ? hash & (uint)(capacity - 1) : hash % (uint)capacity);

    private void InitializeNativeStorage(int capacity, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        capacity = NativeCapacity(capacity);
        var bytes = checked(NativeShellBytes + (capacity == 0 ? 0 : EstimateNativeSlots(0, capacity)));
        using var initial = governor?.ReserveTemporary(bytes, span);
        _nativeItems = capacity == 0 ? [] : new NativeEntry[capacity];
        if (initial is not null)
        {
            initial.Commit(bytes);
            _nativeCommittedBytes = bytes;
        }
    }

    internal static PyDict CopyStorage(IEnumerable<KeyValuePair<object, object>> entries,
        MemoryGovernor? governor, LythonSourceSpan? span)
    {
        if (entries is PyDict source) return new PyDict(source, governor, span);
        var capacity = entries is PyDict dictionary ? dictionary.Count
            : entries.TryGetNonEnumeratedCount(out var count) ? count : 0;
        var copy = new PyDict(capacity, governor, span);
        copy.CopyNativeEntries(entries, governor, span);
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
                EnsureNativeCapacity(checked(_nativeCount + 1), governor, span);
                _ = SetNativeItem(pair.Key, pair.Value);
            }
        }
        catch
        {
            var released = ReleaseNativeStorage();
            if (released > 0) governor!.Release(released);
            throw;
        }
    }

    internal void EnsureNativeCapacity(int targetCount, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var previous = _nativeItems!;
        if (targetCount <= previous.Length) return;
        var capacity = NativeCapacity(targetCount);
        using var replacement = governor?.ReserveTemporary(checked(96L + NativeSlotBytes * capacity), span);
        var items = new NativeEntry[capacity];
        for (var index = 0; index < _nativeCount; index++)
        {
            PyStructuralGuard.NoteWork(null, span);
            ref var entry = ref items[index];
            entry.Key = previous[index].Key;
            entry.Value = previous[index].Value;
            entry.Hash = previous[index].Hash;
            entry.Sequence = previous[index].Sequence;
            entry.Protocol = previous[index].Protocol;
            var bucket = NativeBucket(entry.Hash, capacity);
            entry.Next = items[bucket].Bucket;
            items[bucket].Bucket = index + 1;
        }
        _nativeItems = items;
        if (replacement is not null)
        {
            var bytes = EstimateNativeSlots(previous.Length, capacity);
            replacement.Commit(bytes);
            _nativeCommittedBytes += bytes;
        }
    }

    private int FindNativeIndex(object key)
    {
        if (_nativeCount == 0) return -1;
        if (PyHashProtocols.NeedsProtocolKey(key)) return FindNativeIdentity(key);
        return FindNativeIndex(key, NativeHash(key));
    }

    private int FindNativeIndex(object key, uint hash, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        var items = _nativeItems!;
        if (_nativeCount == 0) return -1;
        var index = items[NativeBucket(hash, items.Length)].Bucket - 1;
        while (index >= 0)
        {
            ref var entry = ref items[index];
            if (entry.Hash == hash && PyValueComparer.Instance.Equals(entry.Key, key)) return index;
            PyStructuralGuard.NoteWork(context, span ?? _allocationSpan);
            index = entry.Next - 1;
        }
        return -1;
    }

    private bool TryGetNativeValue(object key, [MaybeNullWhen(false)] out object value)
    {
        var index = FindNativeIndex(key);
        value = index < 0 ? null : _nativeItems![index].Value!;
        return index >= 0;
    }

    private bool SetNativeItem(object key, object value)
    {
        var before = _nativeCount;
        var context = ActiveProtocolContext();
        var span = QuerySpan;
        var (hash, protocol) = PrepareQueryKey(key, context, span);
        SetNativeValue(key, value, hash, protocol, context, span, replaceExisting: true);
        return _nativeCount != before;
    }

    private void AppendNativeItem(object key, object value, uint hash, bool protocol = false)
    {
        EnsureNativeCapacity(checked(_nativeCount + 1), _memoryGovernor, _allocationSpan);
        var items = _nativeItems!;
        var index = _nativeCount;
        var bucket = NativeBucket(hash, items.Length);
        ref var entry = ref items[index];
        entry.Key = key;
        entry.Value = value;
        entry.Hash = hash;
        entry.Sequence = ++_nativeSequence;
        entry.Protocol = protocol;
        if (protocol) _nativeProtocolCount++;
        entry.Next = items[bucket].Bucket;
        items[bucket].Bucket = index + 1;
        _nativeCount++;
    }

    private bool TrySetNativeExisting(object key, object value)
    {
        var index = FindNativeIndex(key);
        if (index < 0) return false;
        _nativeItems![index].Value = value;
        return true;
    }

    private bool RemoveNativeItem(object key)
    {
        var index = FindNativeIndex(key);
        if (index < 0) return false;
        RemoveNativeAt(index);
        return true;
    }

    private void RemoveNativeAt(int index)
    {
        var items = _nativeItems!;
        if (items[index].Protocol) _nativeProtocolCount--;
        var removed = index + 1;
        ref var link = ref items[NativeBucket(items[index].Hash, items.Length)].Bucket;
        while (link != removed)
        {
            PyStructuralGuard.NoteWork(null, _allocationSpan);
            link = ref items[link - 1].Next;
        }
        link = items[index].Next;
        if (_keysOnly)
        {
            // Sets have no ordered payload contract. Move the last payload into
            // the hole, preserving physical bucket heads and chain order.
            var lastIndex = --_nativeCount;
            if (index != lastIndex)
            {
                var moved = items[lastIndex];
                ref var incoming = ref items[NativeBucket(moved.Hash, items.Length)].Bucket;
                while (incoming != lastIndex + 1) incoming = ref items[incoming - 1].Next;
                incoming = index + 1;
                var bucketHead = items[index].Bucket;
                items[index] = moved;
                items[index].Bucket = bucketHead;
            }
            var preservedHead = items[lastIndex].Bucket;
            items[lastIndex] = default;
            items[lastIndex].Bucket = preservedHead;
            return;
        }
        // Atomic structural mutation, as with the previous BCL removal. Chains
        // descend through ordered indices, so prefix links cannot point forward.
        // Preserve physical bucket heads while shifting only the live suffix.
        for (var from = index + 1; from < _nativeCount; from++)
        {
            ref var source = ref items[from];
            ref var destination = ref items[from - 1];
            var bucket = NativeBucket(source.Hash, items.Length);
            if (items[bucket].Bucket == from + 1) items[bucket].Bucket = from;
            destination.Key = source.Key;
            destination.Value = source.Value;
            destination.Hash = source.Hash;
            destination.Sequence = source.Sequence;
            destination.Protocol = source.Protocol;
            destination.Next = source.Next > removed ? source.Next - 1 : source.Next;
        }
        ref var last = ref items[--_nativeCount];
        last.Key = null;
        last.Value = null;
        last.Hash = 0;
        last.Next = 0;
        last.Sequence = 0;
        last.Protocol = false;
    }

    // Native-format proofs cannot enter bridges, decode text or dispatch protocols.
    internal bool TryReadNativeStringValue(Text.PyString key, [MaybeNullWhen(false)] out object value)
    {
        if (_nativeItems is not null && !HasProtocolKeys) return TryGetNativeValue(key, out value);
        value = null;
        return false;
    }

    // Raw scans are live; Python iteration separately checks dictionary version.
    private IEnumerable<KeyValuePair<object, object>> NativeEntries()
    {
        for (var index = 0; index < _nativeCount; index++)
            yield return new(_nativeItems![index].Key!, _nativeItems[index].Value!);
    }

    private IEnumerable<object> NativeKeys()
    {
        for (var index = 0; index < _nativeCount; index++) yield return _nativeItems![index].Key!;
    }

    private IEnumerable<object> NativeValues()
    {
        for (var index = 0; index < _nativeCount; index++) yield return _nativeItems![index].Value!;
    }

    int IPyDictStorage.Count => _nativeItems is not null ? _nativeCount : _items.Count;
    IEnumerable<object> IPyDictStorage.Keys => _nativeItems is not null ? NativeKeys() : _items.Keys;
    IEnumerable<object> IPyDictStorage.Values => _nativeItems is not null ? NativeValues() : _items.Values;
    IEnumerable<KeyValuePair<object, object>> IPyDictStorage.Entries => _nativeItems is not null ? NativeEntries() : _items.Entries;
    object IPyDictStorage.GetRequired(object key) => _nativeItems is null ? _items.GetRequired(key)
        : TryGetNativeValue(key, out var value) ? value : throw new KeyNotFoundException();
    bool IPyDictStorage.TryGetValue(object key, [MaybeNullWhen(false)] out object value)
        => _nativeItems is not null ? TryGetNativeValue(key, out value) : _items.TryGetValue(key, out value);
    bool IPyDictStorage.ContainsKey(object key) => _nativeItems is not null ? FindNativeIndex(key) >= 0 : _items.ContainsKey(key);
    bool IPyDictStorage.SetItem(object key, object value) => _nativeItems is not null ? SetNativeItem(key, value) : _items.SetItem(key, value);
    bool IPyDictStorage.TrySetExisting(object key, object value) => _nativeItems is not null ? TrySetNativeExisting(key, value) : _items.TrySetExisting(key, value);
    bool IPyDictStorage.HasProtocolKeys => HasProtocolKeys;
    bool IPyDictStorage.TryGetStoredHash(object key, out uint hash)
    {
        var index = FindNativeIdentity(key);
        hash = index < 0 ? 0 : _nativeItems![index].Hash;
        return index >= 0;
    }
    void IPyDictStorage.AddNew(object key, object value, uint? knownHash)
    {
        if (_nativeItems is null) { _items.AddNew(key, value, knownHash); return; }
        var (hash, protocol) = knownHash is uint storedHash
            ? (storedHash, PyHashProtocols.NeedsProtocolKey(key))
            : PrepareQueryKey(key, ActiveProtocolContext(), QuerySpan);
        if (knownHash is null && FindNativeIndex(key, hash) >= 0)
            throw new ArgumentException("An item with the same key has already been added.");
        AppendNativeItem(key, value, hash, protocol);
    }
    bool IPyDictStorage.Remove(object key) => _nativeItems is not null ? RemoveNativeItem(key) : _items.Remove(key);
    void IPyDictStorage.Clear()
    {
        if (_nativeItems is not null) { Array.Clear(_nativeItems); _nativeCount = 0; _nativeProtocolCount = 0; }
        else _items.Clear();
    }
    long IPyDictStorage.CommittedBytes => _nativeItems is not null ? _nativeCommittedBytes : _items.CommittedBytes;
    private long ReleaseNativeStorage()
    {
        if (_nativeItems is null) throw new InvalidOperationException("A live dictionary view retains its backing.");
        var released = _nativeCommittedBytes;
        _nativeCommittedBytes = 0;
        _nativeItems = [];
        _nativeCount = 0;
        _nativeProtocolCount = 0;
        return released;
    }

    private long EstimateNativeSlots(int previousCapacity, int newCapacity)
        => NativeSlotBytes * (newCapacity - previousCapacity) + (previousCapacity == 0 ? 96L : 0L);
}
