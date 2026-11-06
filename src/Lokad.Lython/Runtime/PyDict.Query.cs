namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict
{
    private (uint Hash, bool Protocol) PrepareQueryKey(object key, LythonRuntime.ExecutionContext? context, LythonSourceSpan span)
    {
        try
        {
            if (context is not null)
            {
                var prepared = PyHashProtocols.PrepareKeyHash(key, context, span);
                return (MixNativeHash(prepared.Hash), prepared.Protocol);
            }
            var protocol = PyHashProtocols.NeedsProtocolKey(key);
            if (!protocol) return (NativeHash(key), false);
            // Raw internal consumers can resolve an existing retained object without
            // running guest code. Newly constructed Python keys use an active context.
            var index = _nativeItems is null ? -1 : FindNativeIdentity(key);
            return (index >= 0 ? _nativeItems![index].Hash : NativeHash(key), true);
        }
        catch (PyUnhashableException error) { throw RuntimeErrors.UnhashableType(error.Value, span); }
    }

    private int FindNativeIdentity(object key)
    {
        for (var i = 0; i < _nativeCount; i++)
        {
            PyStructuralGuard.NoteWork(null, _allocationSpan);
            if (ReferenceEquals(_nativeItems![i].Key, key)) return i;
        }
        return -1;
    }

    private int CurrentNativeIndex(in NativeEntry candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_keysOnly)
        {
            if (_nativeCount == 0) return -1;
            var items = _nativeItems!;
            for (var i = items[NativeBucket(candidate.Hash, items.Length)].Bucket - 1; i >= 0; i = items[i].Next - 1)
            {
                PyStructuralGuard.NoteWork(context, span);
                if (items[i].Sequence == candidate.Sequence && ReferenceEquals(items[i].Key, candidate.Key)) return i;
            }
            return -1;
        }
        // Insertion identities stay ordered even when removal compacts payloads.
        // A removed/reinserted object is a different entry, including after clear.
        var low = 0;
        var high = _nativeCount - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var current = _nativeItems![middle];
            if (current.Sequence == candidate.Sequence)
                return ReferenceEquals(current.Key, candidate.Key) ? middle : -1;
            if (current.Sequence < candidate.Sequence) low = middle + 1;
            else high = middle - 1;
        }
        return -1;
    }

    private int NativeCandidateCount(uint hash, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_nativeCount == 0) return 0;
        var count = 0;
        var items = _nativeItems!;
        for (var index = items[NativeBucket(hash, items.Length)].Bucket - 1; index >= 0; index = items[index].Next - 1)
        {
            PyStructuralGuard.NoteWork(context, span);
            if (items[index].Hash == hash) count++;
        }
        return count;
    }

    private NativeEntry[] SnapshotNativeCandidates(uint hash, int count, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (count == 0) return [];
        var candidates = new NativeEntry[count];
        var items = _nativeItems!;
        // Chains are newest first; Python equality visits insertion order.
        for (var index = items[NativeBucket(hash, items.Length)].Bucket - 1; index >= 0; index = items[index].Next - 1)
        {
            PyStructuralGuard.NoteWork(context, span);
            if (items[index].Hash == hash) candidates[--count] = items[index];
        }
        return candidates;
    }

    private int ResolveNativeKey(object key, uint hash, bool protocol, LythonRuntime.ExecutionContext? context,
        LythonSourceSpan span, bool acceptRemovedMatch = false)
    {
        if (!HasProtocolKeys && !protocol) return FindNativeIndex(key, hash, context, span);
        if (context is null)
        {
            var identity = FindNativeIdentity(key);
            return identity >= 0 ? identity : FindNativeIndex(key, hash);
        }
        return ResolveContextualKey(key, hash, protocol, context, span, acceptRemovedMatch);
    }

    private int ResolveContextualKey(object key, uint hash, bool protocol, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, bool acceptRemovedMatch)
    {
        long lastSequence = 0;
        while (true)
        {
            context.CheckExecution(span);
            var before = _nativeSequence;
            var count = NativeCandidateCount(hash, context, span);
            using var storage = _memoryGovernor?.ReserveTemporary(count == 0 ? 0 : checked(32L + NativeSlotBytes * count), span);
            var candidates = SnapshotNativeCandidates(hash, count, context, span);
            foreach (var candidate in candidates)
            {
                context.CheckExecution(span);
                if (candidate.Sequence <= lastSequence) continue;
                lastSequence = candidate.Sequence;
                if (CurrentNativeIndex(candidate, context, span) < 0) continue;
                var matches = ReferenceEquals(candidate.Key, key) || (candidate.Protocol || protocol
                    ? LythonRuntime.ElementEquals(candidate.Key!, key, context, span)
                    : PyValueComparer.Instance.Equals(candidate.Key, key));
                context.CheckExecution(span);
                var index = matches ? CurrentNativeIndex(candidate, context, span) : -1;
                if (index >= 0) return index;
                // Set insertion accepts a positive comparison even if that
                // callback removed its candidate. Lookups require a current entry.
                if (matches && acceptRemovedMatch) return -2;
            }
            if (_nativeSequence == before) return -1;
        }
    }

    private async ValueTask<int> ResolveNativeKeyAsync(object key, uint hash, bool protocol,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        long lastSequence = 0;
        while (true)
        {
            context.CheckExecution(span);
            var before = _nativeSequence;
            var count = NativeCandidateCount(hash, context, span);
            using var storage = _memoryGovernor?.ReserveTemporary(count == 0 ? 0 : checked(32L + NativeSlotBytes * count), span);
            var candidates = SnapshotNativeCandidates(hash, count, context, span);
            foreach (var candidate in candidates)
            {
                context.CheckExecution(span);
                if (candidate.Sequence <= lastSequence) continue;
                lastSequence = candidate.Sequence;
                if (CurrentNativeIndex(candidate, context, span) < 0) continue;
                var matches = ReferenceEquals(candidate.Key, key) || (candidate.Protocol || protocol
                    ? await LythonRuntime.ElementEqualsAsync(candidate.Key!, key, context, span).ConfigureAwait(false)
                    : PyValueComparer.Instance.Equals(candidate.Key, key));
                context.CheckExecution(span);
                var index = matches ? CurrentNativeIndex(candidate, context, span) : -1;
                if (index >= 0) return index;
            }
            if (_nativeSequence == before) return -1;
        }
    }

    private object SetNativeValue(object key, object value, uint hash, bool protocol,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan span, bool replaceExisting)
    {
        var index = ResolveNativeKey(key, hash, protocol, context, span);
        if (index >= 0)
        {
            if (replaceExisting) _nativeItems![index].Value = value;
            return replaceExisting ? value : _nativeItems![index].Value!;
        }
        AppendNativeItem(key, value, hash, protocol);
        return value;
    }
    private bool TryGetBridgeValue(object key, LythonRuntime.ExecutionContext? context,
        LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        using var ambient = context is null ? default : PyStructuralGuard.PushAmbient(context, span);
        var (hash, protocol) = PrepareQueryKey(key, context, span);
        var stored = ResolveBridgeKeyAsync(key, hash, protocol, context, span, asynchronous: false).GetAwaiter().GetResult();
        if (stored is not null) return _items.TryGetValue(stored, out value);
        value = PyNone.Instance; return false;
    }

    // Namespace/environment bridges retain their real value storage. Key metadata
    // comes from their existing native key owner; no shadow table is reconstructed.
    private async ValueTask<object?> ResolveBridgeKeyAsync(object key, uint hash, bool protocol,
        LythonRuntime.ExecutionContext? context, LythonSourceSpan span, bool asynchronous)
    {
        if (context is null || !HasProtocolKeys && !protocol)
        {
            using var ambient = context is null ? default : PyStructuralGuard.PushAmbient(context, span);
            return _items.ContainsKey(key) ? key : null;
        }
        using var storage = _memoryGovernor?.ReserveTemporary(checked(32L + NativeSlotBytes * Count), span);
        var entries = new NativeEntry[Count];
        var index = 0;
        foreach (var pair in _items.Entries)
        {
            context.CheckExecution(span);
            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            var storedHash = _items.TryGetStoredHash(pair.Key, out var foundHash) ? foundHash : NativeHash(pair.Key);
            entries[index++] = new NativeEntry { Key = pair.Key, Value = pair.Value, Hash = storedHash };
        }
        foreach (var entry in entries)
        {
            context.CheckExecution(span);
            if (entry.Hash != hash) continue;
            var storedKey = entry.Key.RequireNotNull();
            var matches = ReferenceEquals(storedKey, key) || (asynchronous
                ? await LythonRuntime.ElementEqualsAsync(storedKey, key, context, span).ConfigureAwait(false)
                : LythonRuntime.ElementEquals(storedKey, key, context, span));
            context.CheckExecution(span);
            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            if (matches && _items.ContainsKey(storedKey)) return storedKey;
        }
        return null;
    }

    internal bool AddSetKey(object key, uint? storedHash = null)
    {
        _memoryGovernor?.ThrowIfAdmissionFailed();
        var context = ActiveProtocolContext();
        var span = QuerySpan;
        var (hash, protocol) = storedHash is uint knownHash
            ? (knownHash, PyHashProtocols.NeedsProtocolKey(key))
            : PrepareQueryKey(key, context, span);
        if (ResolveNativeKey(key, hash, protocol, context, span, acceptRemovedMatch: true) != -1) return false;
        AppendNativeItem(key, PyNone.Instance, hash, protocol);
        _version++;
        return true;
    }

    internal bool ContainsStoredKey(object key, uint hash, LythonRuntime.ExecutionContext? context, LythonSourceSpan span)
        => ResolveNativeKey(key, hash, PyHashProtocols.NeedsProtocolKey(key), context, span) >= 0;

    internal async ValueTask<bool> ContainsStoredKeyAsync(object key, uint hash,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => await ResolveNativeKeyAsync(key, hash, PyHashProtocols.NeedsProtocolKey(key), context, span).ConfigureAwait(false) >= 0;

    internal bool RemoveStoredHashKey(object key, uint hash)
    {
        var index = ResolveNativeKey(key, hash, PyHashProtocols.NeedsProtocolKey(key), ActiveProtocolContext(), QuerySpan);
        if (index < 0) return false;
        RemoveNativeAt(index); _version++;
        return true;
    }

    internal IEnumerable<(object Key, uint Hash)> StoredKeys()
    {
        var version = _version;
        for (var i = 0; ; i++)
        {
            EnsureUnmodified(version);
            if (i == _nativeCount) yield break;
            var entry = _nativeItems![i];
            yield return (entry.Key!, entry.Hash);
        }
    }

    internal object StoredKeyAt(int index) => _nativeItems![index].Key!;

    internal void ClearSetStorage()
    {
        if (_nativeCount != 0) _version++;
        _nativeItems = [];
        _nativeCount = _nativeProtocolCount = 0;
        if (_memoryGovernor is not null)
        {
            var released = _nativeCommittedBytes - NativeShellBytes;
            _nativeCommittedBytes = NativeShellBytes;
            if (released > 0) _memoryGovernor.Release(released);
        }
    }

    internal static PyDict CreateSetStorage(MemoryGovernor? governor, LythonSourceSpan? span)
        => new(0, governor, span, keysOnly: true);

    internal long KeySnapshotBytes => checked(32L + (NativeEntryBytes + 64L) * Count);

    internal bool PopSetKey(out object key)
    {
        if (_nativeCount == 0) { key = PyNone.Instance; return false; }
        key = _nativeItems![0].Key!;
        RemoveNativeAt(0); _version++;
        return true;
    }

    internal bool RemoveSetEntry(object key, uint hash)
    {
        if (_nativeCount == 0) return false;
        var items = _nativeItems!;
        for (var i = items[NativeBucket(hash, items.Length)].Bucket - 1; i >= 0; i = items[i].Next - 1)
        {
            PyStructuralGuard.NoteWork(null, _allocationSpan);
            if (!ReferenceEquals(items[i].Key, key)) continue;
            RemoveNativeAt(i); _version++;
            return true;
        }
        return false;
    }

}
