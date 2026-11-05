using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict : IEnumerable<KeyValuePair<object, object>>, IPyDictStorage, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue, IChainMapSource, IPyOwnershipSnapshot
{
    private IPyDictStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private int _version;
    internal void NotifyNamespaceMutation() => _version++;
    // Escaped views and iterators retain the dictionary and its storage governor.
    // Ownership snapshots report the current conservative storage estimate.

    // Protocol-key side table (R13b): keys needing __hash__/__eq__ dispatch
    // bypass Dictionary probing (which cannot run guest code) and scan with
    // precomputed hashes plus element ==. The fast store keeps every entry;
    // the side list only shadows protocol keys (insertion sequence, hash and
    // key) for order-preserving scans, so iteration order stays exact and
    // later ops stay unified. (N10: snapshots preflighted, scans budgeted,
    // side candidates indexed by protocol hash.)
    private List<ProtocolEntry>? _protocol;
    private ProtocolSideIndex? _sideIndex;
    private long _protocolBytes;
    private long _protocolSeq;

    // StructuralHash is frozen at insert: the comparer never dispatches guest
    // code, so it cannot change under a key object and scans never recompute it.
    // Layout stays 24 bytes (long + int + int + reference), covered by the
    // existing per-entry rate.
    internal readonly record struct ProtocolEntry(long Seq, int Hash, int StructuralHash, object Key) : IProtocolSideEntry;

    internal const long ProtocolEntryBytes = 64;

    public PyDict() : this(0, null, null) { }

    public PyDict(MemoryGovernor governor) : this(governor, null) { }

    internal PyDict(IPyDictStorage storage, MemoryGovernor governor, LythonSourceSpan allocationSpan)
    {
        _items = storage;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public PyDict(MemoryGovernor governor, LythonSourceSpan? allocationSpan) : this(0, governor, allocationSpan) { }

    internal PyDict(int capacity, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        InitializeNativeStorage(capacity, governor, allocationSpan);
        _items = this;
    }

    public PyDict(PyDict other) : this(other, other._memoryGovernor, other._allocationSpan) { }

    public PyDict(PyDict other, MemoryGovernor governor) : this(other, governor, null) { }

    public PyDict(PyDict other, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        InitializeNativeStorage(other.Count, governor, allocationSpan);
        _items = this;
        CopyNativeEntries(other._items.Entries, governor, allocationSpan);
        _protocolSeq = other._protocolSeq;
        try
        {
            var protocolBytes = checked((long)(other._protocol?.Count ?? 0) * ProtocolEntryBytes);
            governor?.Reserve(protocolBytes, allocationSpan);
            governor?.Commit(protocolBytes);
            _protocolBytes = governor is null ? 0 : protocolBytes;
            _protocol = other._protocol is null ? null : new List<ProtocolEntry>(other._protocol);
            if (other._sideIndex is not null)
            {
                _sideIndex = new ProtocolSideIndex();
                _sideIndex.CloneFrom(other._sideIndex, governor, allocationSpan);
            }
        }
        catch
        {
            RefundAbortedConstruction();
            throw;
        }
    }

    // The side index holds shadows of fast-store entries, never additional
    // entries, so only the fast store counts. Its backing bytes still add
    // to CommittedStorageBytes below.
    public int Count => _items.Count;

    object IChainMapSource.Underlying => this;

    // Current backing funding for unpublished rollback and explicit replacement.
    // Dropping a guest reference does not refund this batch-owned storage.
    // Retained table capacity includes pair headroom. Larger payloads keep
    // their separate owners; aliases never require identity bookkeeping.
    internal long CommittedStorageBytes => _items.CommittedBytes + _protocolBytes + (_sideIndex?.CommittedBytes ?? 0);

    public int Length => Count;

    internal bool HasProtocolKeys => _protocol is { Count: > 0 };

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public IEnumerable<object> Keys
    {
        get => EnumerateKeys(_version);
    }

    private IEnumerable<object> EnumerateKeys(int expectedVersion)
    {
        using var enumerator = _items.Keys.GetEnumerator();
        while (true)
        {
            EnsureUnmodified(expectedVersion);
            if (!enumerator.MoveNext())
            {
                yield break;
            }

            yield return enumerator.Current;
        }
    }

    public IEnumerable<object> Values
    {
        get => EnumerateValues(_version);
    }

    private IEnumerable<object> EnumerateValues(int expectedVersion)
    {
        using var enumerator = _items.Values.GetEnumerator();
        while (true)
        {
            EnsureUnmodified(expectedVersion);
            if (!enumerator.MoveNext())
            {
                yield break;
            }

            yield return enumerator.Current;
        }
    }

    public IEnumerable<KeyValuePair<object, object>> Items => this;

    public object GetItem(object key) => _items.GetRequired(key);

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value)
    {
        if (HasProtocolKeys && !PyHashProtocols.NeedsProtocolKey(key) &&
            ActiveProtocolContext() is { } context && (PyStructuralGuard.AmbientSpan ?? _allocationSpan) is { } span)
            return TryGetBuiltinValue(key, context, span, out value);

        if (_items.TryGetValue(key, out var stored)) { value = stored; return true; }
        if (PyHashProtocols.NeedsProtocolKey(key)) return TryGetProtocolValue(key, out value);
        value = PyNone.Instance;
        return false;
    }

    // Hop-proof lookup for async == (ambient ThreadStatics do not survive
    // thread hops between awaits); synchronous paths rely on the ambient
    // branch inside TryGetProtocolValue instead.
    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (HasProtocolKeys && !PyHashProtocols.NeedsProtocolKey(key))
            return TryGetBuiltinValue(key, context, span, out value);
        if (_items.TryGetValue(key, out var stored)) { value = stored; return true; }
        if (PyHashProtocols.NeedsProtocolKey(key)) return TryGetProtocolValueExplicit(key, context, span, out value);
        value = PyNone.Instance;
        return false;
    }

    // Native keys are already validated by guest-facing callers. Only equality
    // with stored protocol keys can suspend; hashing stays on its native path.
    internal ValueTask<(bool Found, object Value)> TryGetBuiltinKeyAsync(
        object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!HasProtocolKeys)
            return new(_items.TryGetValue(key, out var value) ? (true, value) : (false, PyNone.Instance));
        return LookupAsync();

        async ValueTask<(bool Found, object Value)> LookupAsync()
        {
            var entry = await ResolveBuiltinKeyAsync(key, context, span, asynchronous: true).ConfigureAwait(false);
            return (entry.Key is not null, entry.Value);
        }
    }

    internal bool TryGetStringValue(string name, MemoryGovernor governor, LythonSourceSpan? span,
        LythonRuntime.ExecutionContext? context, [MaybeNullWhen(false)] out object value)
    {
        var key = PyString.FromString(name, governor, span);
        var mayEscape = HasProtocolKeys;
        try { return context is null ? TryGetValue(key, out value) : TryGetValue(key, out value, context, span ?? new(0, 0, 0, 0)); }
        finally { if (!mayEscape) governor.RefundUnpublishedValue(key); }
    }

    internal async ValueTask<(bool Found, object Value)> TryGetStringValueAsync(string name,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var governor = context.MemoryGovernor;
        var key = PyString.FromString(name, governor, span);
        var mayEscape = HasProtocolKeys;
        try { return await TryGetBuiltinKeyAsync(key, context, span).ConfigureAwait(false); }
        finally { if (!mayEscape) governor.RefundUnpublishedValue(key); }
    }

    internal void SetStringItem(string name, object value, MemoryGovernor governor, LythonSourceSpan? span)
    {
        var key = PyString.FromString(name, governor, span);
        var mayEscape = HasProtocolKeys;
        var before = Count;
        try { SetItem(key, value); }
        finally { if (!mayEscape && Count == before) governor.RefundUnpublishedValue(key); }
    }

    public bool ContainsKey(object key) => TryGetValue(key, out _);

    public void SetItem(object key, object value)
    {
        if (PyHashProtocols.NeedsProtocolKey(key))
        {
            _ = SetProtocolItem(key, value, replaceExisting: true);
            return;
        }

        if (HasProtocolKeys && ActiveProtocolContext() is { } context &&
            (PyStructuralGuard.AmbientSpan ?? _allocationSpan) is { } span)
        {
            var entry = ResolveBuiltinKeyAsync(key, context, span, asynchronous: false).GetAwaiter().GetResult();
            if (entry.Key is not null) StoreItem(entry.Key, value);
            else StoreItem(key, value);
            return;
        }
        StoreItem(key, value);
    }

    // Native mapping merges reuse stored protocol hashes, as Python dict/set
    // copying does. The side list follows the fast store's insertion order.
    // This remains a private key-only copy; no source table or entry escapes.
    internal void AddKeysFrom(PyDict source, LythonRuntime.ExecutionContext? context, LythonSourceSpan span)
    {
        using var ambient = context is null ? default : PyStructuralGuard.PushAmbient(context, span);
        using var entries = source._items.Entries.GetEnumerator();
        var expectedVersion = source._version;
        var protocolIndex = 0;
        while (true)
        {
            context?.CheckExecution(span);
            source.EnsureUnmodified(expectedVersion);
            if (!entries.MoveNext()) break;
            var key = entries.Current.Key;
            if (PyHashProtocols.NeedsProtocolKey(key))
            {
                int? hash = null;
                if (source._protocol is { } protocols && protocolIndex < protocols.Count &&
                    ReferenceEquals(protocols[protocolIndex].Key, entries.Current.Key))
                    hash = protocols[protocolIndex++].Hash;
                _ = SetProtocolItem(key, PyNone.Instance, replaceExisting: false, hash);
            }
            else if (context is null ? !TryGetValue(key, out _) : !TryGetValue(key, out _, context, span))
                SetItem(key, PyNone.Instance);
        }
    }

    internal object SetDefault(object key, object value, MemoryGovernor governor, LythonSourceSpan span)
    {
        if (ActiveProtocolContext() is { } context)
        {
            if (PyHashProtocols.NeedsProtocolKey(key))
                return SetProtocolItem(key, value, replaceExisting: false);
            if (HasProtocolKeys)
            {
                var entry = ResolveBuiltinKeyAsync(key, context, span, asynchronous: false).GetAwaiter().GetResult();
                if (entry.Key is not null) return entry.Value;
                AttachMemoryGovernor(governor, span);
                StoreItem(key, value);
                return value;
            }
        }
        if (TryGetValue(key, out var found)) return found;
        if (_items.TryGetValue(key, out var stored)) return stored;
        AttachMemoryGovernor(governor, span);
        SetItem(key, value);
        return value;
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (_memoryGovernor is not null)
        {
            _allocationSpan ??= allocationSpan;
            return;
        }
        var span = _allocationSpan ?? allocationSpan;
        PyDict? storage = null;
        ProtocolSideIndex? sideIndex = null;
        var protocolBytes = checked((long)(_protocol?.Count ?? 0) * ProtocolEntryBytes);
        governor.Reserve(protocolBytes, span);
        try
        {
            using var scratch = governor.ReserveTemporary(checked(16L * Count), span);
            storage = CopyStorage(_items, governor, span);
            if (_sideIndex is not null)
            {
                sideIndex = new ProtocolSideIndex();
                sideIndex.CloneFrom(_sideIndex, governor, span);
            }
        }
        catch
        {
            governor.ReleaseReserved(protocolBytes);
            if (storage is not null) governor.Release(storage.ReleaseNativeStorage());
            sideIndex?.ReleaseAll(governor);
            throw;
        }
        governor.Commit(protocolBytes);
        _nativeItems = storage._nativeItems;
        _nativeCount = storage._nativeCount;
        _nativeCommittedBytes = storage._nativeCommittedBytes;
        storage._nativeItems = null;
        storage._nativeCount = 0;
        storage._nativeCommittedBytes = 0;
        storage._items = null!;
        _items = this;
        _sideIndex = sideIndex;
        _protocolBytes = protocolBytes;
        _memoryGovernor = governor;
        _allocationSpan = span;
    }

    // Native unwind uses the retained registry key without allocating or running
    // guest hash/equality callbacks after cancellation or refused admission.
    internal void RemoveStoredKey(object key)
    {
        if (_items.TryGetValue(key, out var stored))
            RemoveResolvedEntry(key);
    }

    public bool Remove(object key)
    {
        if (HasProtocolKeys && !PyHashProtocols.NeedsProtocolKey(key) &&
            ActiveProtocolContext() is { } context && (PyStructuralGuard.AmbientSpan ?? _allocationSpan) is { } span)
            return TryRemoveBuiltinValue(key, context, span, out _);
        if (_items.TryGetValue(key, out var storedValue))
        {
            _ = _items.Remove(key);
            RemoveProtocolShadow(key);
            _version++;
            return true;
        }

        if (RemoveProtocolItem(key))
        {
            _version++;
            return true;
        }

        return false;
    }

    // Single lookup-and-remove for pop: resolves the stored entry once and
    // deletes it without re-dispatching guest code. A lookup followed by a
    // separate Remove would run __eq__/__hash__ a second time, so a mutation
    // inside the first dispatch could fail spuriously or double-apply.
    public bool TryRemove(object key, [MaybeNullWhen(false)] out object value)
    {
        if (HasProtocolKeys && !PyHashProtocols.NeedsProtocolKey(key) &&
            ActiveProtocolContext() is { } context && (PyStructuralGuard.AmbientSpan ?? _allocationSpan) is { } span)
            return TryRemoveBuiltinValue(key, context, span, out value);
        // Ordinary storage probes stay direct; mixed native keys have already
        // resolved any earlier protocol candidates above.
        if (_items.TryGetValue(key, out var stored))
        {
            value = stored;
            RemoveResolvedEntry(key);
            return true;
        }

        if (PyHashProtocols.NeedsProtocolKey(key))
        {
            return TryRemoveProtocolValue(key, out value);
        }

        value = PyNone.Instance;
        return false;
    }

    // Deletes an already-resolved stored entry: no guest dispatch, version
    // bumped exactly once. Entry funding is independent
    // of the identity or type of the lookup argument.
    private void RemoveResolvedEntry(object key)
    {
        _ = _items.Remove(key);
        RemoveProtocolShadow(key);
        _version++;
    }

    private bool TryRemoveProtocolValue(object key, [MaybeNullWhen(false)] out object value)
    {
        var context = ActiveProtocolContext();
        if (context is null)
        {
            return TryRemoveProtocolStructural(key, out value);
        }

        return TryRemoveProtocolValueExplicit(key, context, PyStructuralGuard.AmbientSpan ?? _allocationSpan, out value);
    }

    private bool TryRemoveProtocolValueExplicit(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan? span, [MaybeNullWhen(false)] out object value)
    {
        var useSpan = span ?? _allocationSpan;
        if (useSpan is null)
        {
            return TryRemoveProtocolStructural(key, out value);
        }

        var hash = PyHashProtocols.GetProtocolHash(key, context, useSpan);
        var structuralHash = PyValueComparer.Instance.GetHashCode(key);
        // Same dispatch set and order as the lookup twin (N10 index): the side
        // phase scans hash-bucketed candidates, then the store phase below.
        var work = 0;
        if (_protocol is not null)
        {
            using var candidatesStorage = _memoryGovernor?.ReserveTemporary(
                ProtocolSideIndex.EstimateSnapshotBytes(_protocol?.Count ?? 0, ProtocolEntryBytes), useSpan);
            var candidates = ProtocolSideIndex.SnapshotCandidates(_protocol, _sideIndex, hash, structuralHash, builtinOnly: false, useSpan, context);
            for (var i = 0; i < candidates.Length; i++)
            {
                if ((++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var entry = candidates[i];
                if (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan))
                {
                    // The match may have been deleted by an earlier callback in
                    // this same scan (CPython then reports the key missing, so
                    // the scan continues instead of failing).
                    if (_items.TryGetValue(entry.Key, out var stored))
                    {
                        value = stored;
                        RemoveResolvedEntry(entry.Key);
                        return true;
                    }
                }
            }
        }

        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items.Entries)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecution(useSpan);
            }

            if (PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash)
            {
                storeHit = probe;
                break;
            }

            probe++;
        }

        if (storeHit >= 0)
        {
            using var snapshot = _memoryGovernor?.ReserveTemporary(checked(32L + (16L * _items.Count)), useSpan);
            var storeSnapshot = _items.Entries.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                // The hit entry already consumed its budget check above.
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var pair = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash) && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(pair.Key, key, context, useSpan)))
                {
                    if (_items.TryGetValue(pair.Key, out var stored))
                    {
                        value = stored;
                        RemoveResolvedEntry(pair.Key);
                        return true;
                    }
                }
            }
        }

        value = PyNone.Instance;
        return false;
    }

    private bool TryRemoveBuiltinValue(object key, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        var entry = ResolveBuiltinKeyAsync(key, context, span, asynchronous: false).GetAwaiter().GetResult();
        value = entry.Value;
        if (entry.Key is null) return false;
        RemoveResolvedEntry(entry.Key);
        return true;
    }

    private bool TryRemoveProtocolStructural(object key, [MaybeNullWhen(false)] out object value)
    {
        if (_protocol is not null)
        {
            for (var i = 0; i < _protocol.Count; i++)
            {
                var entry = _protocol[i];
                if (ReferenceEquals(entry.Key, key) && _items.TryGetValue(entry.Key, out var stored))
                {
                    value = stored;
                    RemoveResolvedEntry(entry.Key);
                    return true;
                }
            }
        }

        value = PyNone.Instance;
        return false;
    }

    // Active protocol context for side-table dispatch: ambient provenance
    // when present and unsuppressed, otherwise null for the structural
    // identity fallback. Callers on async paths use the explicit overloads
    // instead (ambient ThreadStatics do not survive thread hops).
    private LythonRuntime.ExecutionContext? ActiveProtocolContext()
        => PyStructuralGuard.GuestDispatchSuppressed ? null : PyStructuralGuard.AmbientContext;

    // Dual-home protocol lookup: the fast store probes identically first
    // (covering same-object hits in every mode), then the side index scans
    // by identity always and by element == when a context is active. Hash
    // prechecks are semantic (unequal hashes never compare, like CPython
    // buckets), not just an optimization.
    private bool TryGetProtocolValue(object key, [MaybeNullWhen(false)] out object value)
    {
        var context = ActiveProtocolContext();
        if (context is null)
        {
            return TryGetProtocolStructural(key, out value);
        }

        return TryGetProtocolValueExplicit(key, context, PyStructuralGuard.AmbientSpan ?? _allocationSpan, out value);
    }

    private bool TryGetProtocolValueExplicit(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan? span, [MaybeNullWhen(false)] out object value)
    {
        var useSpan = span ?? _allocationSpan;
        if (useSpan is null)
        {
            return TryGetProtocolStructural(key, out value);
        }

        var hash = PyHashProtocols.GetProtocolHash(key, context, useSpan);
        var structuralHash = PyValueComparer.Instance.GetHashCode(key);
        // N10 index: the side phase scans only hash-bucketed candidates in insertion order
        // (same dispatch set and order as the linear scan, so guest == observes the same
        // sequence over a pre-dispatch snapshot); the store phase below is untouched.
        // Candidate copies preflight only candidates; work checks run where dispatch happens.
        var work = 0;
        if (_protocol is not null)
        {
            using var candidatesStorage = _memoryGovernor?.ReserveTemporary(
                ProtocolSideIndex.EstimateSnapshotBytes(_protocol?.Count ?? 0, ProtocolEntryBytes), useSpan);
            var candidates = ProtocolSideIndex.SnapshotCandidates(_protocol, _sideIndex, hash, structuralHash, builtinOnly: false, useSpan, context);
            for (var i = 0; i < candidates.Length; i++)
            {
                if ((++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var entry = candidates[i];
                if (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan))
                {
                    if (_items.TryGetValue(entry.Key, out var stored))
                    {
                        value = stored;
                        return true;
                    }
                }
            }
        }

        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items.Entries)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecution(useSpan);
            }

            if (PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash)
            {
                storeHit = probe;
                break;
            }

            probe++;
        }

        if (storeHit >= 0)
        {
            using var snapshot = _memoryGovernor?.ReserveTemporary(checked(32L + (16L * _items.Count)), useSpan);
            var storeSnapshot = _items.Entries.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                // The hit entry already consumed its budget check above.
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var pair = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash) && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(pair.Key, key, context, useSpan)))
                {
                    value = pair.Value;
                    return true;
                }
            }
        }

        value = PyNone.Instance;
        return false;
    }

    // Structural side scan for context-free paths: identity only, so
    // internal flows (popitem, storage copies) keep working on side-homed
    // keys without dispatching guest code.
    private bool TryGetProtocolStructural(object key, [MaybeNullWhen(false)] out object value)
    {
        if (_protocol is not null)
        {
            foreach (var entry in _protocol)
            {
                if (ReferenceEquals(entry.Key, key) && _items.TryGetValue(entry.Key, out var stored))
                {
                    value = stored;
                    return true;
                }
            }
        }

        value = PyNone.Instance;
        return false;
    }

    // Synchronous entry into the shared native-key resolver.
    private bool TryGetBuiltinValue(object key, LythonRuntime.ExecutionContext context,
        LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
    {
        var entry = ResolveBuiltinKeyAsync(key, context, span, asynchronous: false).GetAwaiter().GetResult();
        value = entry.Value;
        return entry.Key is not null;
    }

    // Only protocol entries need frozen hashes/sequence numbers. Native storage
    // supplies their current relative position without a second key ledger.
    private async ValueTask<(object? Key, object Value)> ResolveBuiltinKeyAsync(object key,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var hash = PyValueComparer.Instance.GetHashCode(key);
        long lastSequence = -1;
        while (true)
        {
            context.CheckExecution(span);
            var sequenceBefore = _protocolSeq;
            using var storage = _memoryGovernor?.ReserveTemporary(
                ProtocolSideIndex.EstimateSnapshotBytes(_protocol?.Count ?? 0, ProtocolEntryBytes), span);
            var candidates = ProtocolSideIndex.SnapshotCandidates(_protocol, _sideIndex,
                hash, hash, builtinOnly: true, span, context);
            foreach (var entry in candidates)
            {
                context.CheckExecution(span);
                if (entry.Seq <= lastSequence) continue;
                lastSequence = entry.Seq;
                var candidateIndex = StorageKeyIndex(entry.Key, context, span);
                if (candidateIndex < 0) continue;
                var nativeIndex = StorageKeyIndex(key, context, span);
                if (nativeIndex >= 0 && nativeIndex < candidateIndex)
                    return (key, _items.GetRequired(key));
                var matches = ReferenceEquals(entry.Key, key) || (asynchronous
                    ? await LythonRuntime.ElementEqualsAsync(entry.Key, key, context, span).ConfigureAwait(false)
                    : LythonRuntime.ElementEquals(entry.Key, key, context, span));
                context.CheckExecution(span);
                // Removal/reinsertion of the same key object is a new entry.
                // Do not return its value using an earlier comparison's rank.
                if (matches && IsCurrentProtocolEntry(entry) && _items.TryGetValue(entry.Key, out var value))
                    return (entry.Key, value);
            }
            if (_protocolSeq != sequenceBefore) continue;
            return _items.TryGetValue(key, out var native) ? (key, native) : (null, PyNone.Instance);
        }
    }

    private int StorageKeyIndex(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_nativeItems is not null) return FindNativeIndex(key);
        // Live namespace bridges expose their existing order, without adding
        // another index or materializing their values.
        var index = 0;
        foreach (var stored in _items.Keys)
        {
            context.CheckExecution(span);
            if (PyValueComparer.Instance.Equals(stored, key)) return index;
            index++;
        }
        return -1;
    }

    private bool IsCurrentProtocolEntry(ProtocolEntry entry)
    {
        var low = 0;
        var high = (_protocol?.Count ?? 0) - 1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            var current = _protocol![middle];
            if (current.Seq == entry.Seq) return ReferenceEquals(current.Key, entry.Key);
            if (current.Seq < entry.Seq) low = middle + 1;
            else high = middle - 1;
        }
        return false;
    }

    // Ensures the side table and its candidate index exist; entries are charged per slot below.
    // The index stays lazy so ordinary-key paths pay nothing.
    private void EnsureSideTable()
    {
        _protocol ??= new List<ProtocolEntry>();
        _sideIndex ??= new ProtocolSideIndex();
    }

    private void ReleaseSideBytes(long bytes)
    {
        if (_memoryGovernor is null || bytes <= 0)
        {
            return;
        }

        _memoryGovernor.Release(bytes);
        _protocolBytes -= bytes;
    }

    // Protocol insert: update in place wherever the logical key already
    // lives (fast or side, retaining the original key object like CPython)
    // and index the stored key; only genuinely new keys append.
    private object SetProtocolItem(object key, object value, bool replaceExisting, int? knownHash = null)
    {
        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        var storageValue = value;
        if (context is null || useSpan is null)
        {
            SetProtocolItemStructural(key, storageValue);
            return value;
        }

        var hash = knownHash ?? PyHashProtocols.GetProtocolHash(key, context, useSpan);
        var structuralHash = PyValueComparer.Instance.GetHashCode(key);
        context.CheckExecution(useSpan);
        // Queries hash once; native merges can supply a stored hash. CLR probing
        // never invokes guest code, and a callback may already have inserted it.
        if (!replaceExisting && _items.TryGetValue(key, out var same))
            return same;

        // Existing insertion sequence numbers let a callback append replacement
        // keys without replaying equality against candidates already examined.
        long lastSequence = -1;
        while (true)
        {
            context.CheckExecution(useSpan);
            var sequenceBefore = _protocolSeq;
            if (_protocol is not null)
            {
                using var candidatesStorage = _memoryGovernor?.ReserveTemporary(
                    ProtocolSideIndex.EstimateSnapshotBytes(_protocol.Count, ProtocolEntryBytes), useSpan);
                var candidates = ProtocolSideIndex.SnapshotCandidates(_protocol, _sideIndex, hash, structuralHash, builtinOnly: false, useSpan, context);
                foreach (var entry in candidates)
                {
                    context.CheckExecution(useSpan);
                    if (entry.Seq <= lastSequence) continue;
                    lastSequence = entry.Seq;
                    var storageKey = entry.Key;
                    if (!_items.ContainsKey(storageKey)) continue;
                    var match = ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan);
                    context.CheckExecution(useSpan);
                    if (match && _items.TryGetValue(storageKey, out var stored))
                    {
                        if (replaceExisting) StoreItem(storageKey, storageValue);
                        return replaceExisting ? value : stored;
                    }
                }
            }
            if (_protocolSeq != sequenceBefore) continue;

            // Protocol entries were already examined through the index. Only
            // native keys can require the structural-store fallback now.
            var nativeMatch = false;
            foreach (var pair in _items.Entries)
            {
                context.CheckExecution(useSpan);
                if (!PyHashProtocols.NeedsProtocolKey(pair.Key) &&
                    (PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash))
                {
                    nativeMatch = true;
                    break;
                }
            }

            var removedNativeMatch = false;
            if (nativeMatch)
            {
                using var snapshot = _memoryGovernor?.ReserveTemporary(checked(32L + 16L * _items.Count), useSpan);
                var storeSnapshot = _items.Entries.ToArray();
                foreach (var pair in storeSnapshot)
                {
                    context.CheckExecution(useSpan);
                    if (PyHashProtocols.NeedsProtocolKey(pair.Key) ||
                        (PyValueComparer.Instance.GetHashCode(pair.Key) != hash && PyValueComparer.Instance.GetHashCode(pair.Key) != structuralHash)) continue;
                    var match = ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(pair.Key, key, context, useSpan);
                    context.CheckExecution(useSpan);
                    if (!match) continue;
                    if (_items.TryGetValue(pair.Key, out var stored))
                    {
                        if (replaceExisting) StoreItem(pair.Key, storageValue);
                        return replaceExisting ? value : stored;
                    }
                    removedNativeMatch = true;
                }
            }
            if (removedNativeMatch || _protocolSeq != sequenceBefore) continue;

            if (!replaceExisting) AttachMemoryGovernor(context.MemoryGovernor, useSpan);
            AppendProtocolItem(key, storageValue, hash, structuralHash);
            return value;
        }
    }

    // Structural protocol insert for context-free paths: identity scan,
    // then a blind dual-home append (later contextual ops reconcile by
    // identity and dispatch).
    private void SetProtocolItemStructural(object key, object storageValue)
    {
        if (_protocol is not null)
        {
            foreach (var entry in _protocol)
            {
                if (ReferenceEquals(entry.Key, key))
                {
                    StoreItem(entry.Key, storageValue);
                    return;
                }
            }
        }

        var structuralHash = PyValueComparer.Instance.GetHashCode(key);
        AppendProtocolItem(key, storageValue, structuralHash, structuralHash);
    }

    private void AppendProtocolItem(object key, object storageValue, int hash, int structuralHash)
    {
        var storageKey = key;
        var protocolBytes = _memoryGovernor is null ? 0 : ProtocolEntryBytes;
        var bytes = protocolBytes;
        _memoryGovernor?.Reserve(bytes, _allocationSpan);
        var countBefore = Count;
        try
        {
            EnsureSideTable();
            var position = _protocol!.Count;
            _sideIndex!.Add(position, hash, structuralHash, _memoryGovernor, _allocationSpan);
            try
            {
                if (_nativeItems is not null) EnsureNativeCapacity(checked(Count + 1), _memoryGovernor, _allocationSpan);
                _items.AddNew(storageKey, storageValue);
                _protocol.Add(new ProtocolEntry(_protocolSeq++, hash, structuralHash, storageKey));
            }
            catch
            {
                if (Count == countBefore) _sideIndex.RemoveForRollback(position, hash, structuralHash, _memoryGovernor);
                throw;
            }
        }
        finally
        {
            if (Count > countBefore)
            {
                _memoryGovernor?.Commit(bytes);
                _protocolBytes += protocolBytes;
                _version++;
            }
            else _memoryGovernor?.ReleaseReserved(bytes);
        }
    }

    // Removes a protocol key wherever it lives; returns whether anything
    // was removed.
    private bool RemoveProtocolItem(object key)
    {
        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        if (context is null || useSpan is null)
        {
            return RemoveProtocolStructural(key);
        }

        // N10 part 1: with no protocol entries homed, a builtin key that
        // missed the structural probe cannot match anything the scan would
        // reach without guest dispatch, so skip both snapshots entirely.
        // (A protocol key still scans the store: an always-true __eq__ can
        // match a builtin-homed entry like CPython.)
        if ((_protocol is null || _protocol.Count == 0) && !PyHashProtocols.NeedsProtocolKey(key))
        {
            return false;
        }

        var hash = PyHashProtocols.GetProtocolHash(key, context, useSpan);
        var structuralHash = PyValueComparer.Instance.GetHashCode(key);
        // N10 index: removal scans only hash-bucketed candidates (same dispatch set and
        // order); the store phase below is untouched. Position fixup rides RemoveSideEntry.
        var work = 0;
        if (_protocol is not null)
        {
            using var candidatesStorage = _memoryGovernor?.ReserveTemporary(
                ProtocolSideIndex.EstimateSnapshotBytes(_protocol?.Count ?? 0, ProtocolEntryBytes), useSpan);
            var candidates = ProtocolSideIndex.SnapshotCandidates(_protocol, _sideIndex, hash, structuralHash, builtinOnly: false, useSpan, context);
            for (var i = 0; i < candidates.Length; i++)
            {
                if ((++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var entry = candidates[i];
                if (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan))
                {
                    RemoveSideEntry(entry.Key);
                    return true;
                }
            }
        }

        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items.Entries)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecution(useSpan);
            }

            if (PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash)
            {
                storeHit = probe;
                break;
            }

            probe++;
        }

        if (storeHit >= 0)
        {
            using var snapshot = _memoryGovernor?.ReserveTemporary(checked(32L + (16L * _items.Count)), useSpan);
            var storeSnapshot = _items.Entries.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var pair = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash) && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(pair.Key, key, context, useSpan)))
                {
                    _items.Remove(pair.Key);
                    RemoveProtocolShadow(pair.Key);
                    return true;
                }
            }
        }

        return false;
    }

    private bool RemoveProtocolStructural(object key)
    {
        if (_protocol is not null)
        {
            for (var i = 0; i < _protocol.Count; i++)
            {
                if (ReferenceEquals(_protocol[i].Key, key))
                {
                    RemoveSideEntry(_protocol[i].Key);
                    return true;
                }
            }
        }

        return false;
    }

    // Prunes a side shadow after the fast entry was removed directly.
    private void RemoveProtocolShadow(object key)
    {
        if (_protocol is null)
        {
            return;
        }

        for (var i = 0; i < _protocol.Count; i++)
        {
            if (ReferenceEquals(_protocol[i].Key, key))
            {
                var shadow = _protocol[i];
                _protocol.RemoveAt(i);
                _sideIndex?.RemoveAt(i, shadow.Hash, shadow.StructuralHash);
                ReleaseSideBytes(ProtocolEntryBytes);
                return;
            }
        }
    }

    private void RemoveSideEntry(object key)
    {
        // An equality callback may already have removed the matched entry.
        // Only a successful deletion can remove this entry's protocol shadow.
        if (!_items.Remove(key)) return;
        RemoveProtocolShadow(key);
    }

    public bool TryRemoveLast([MaybeNullWhen(false)] out object key, [MaybeNullWhen(false)] out object value)
    {
        key = PyNone.Instance;
        value = PyNone.Instance;
        var found = false;
        foreach (var pair in this)
        {
            key = pair.Key;
            value = pair.Value;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        RemoveResolvedEntry(key);
        return true;
    }

    public void Clear()
    {
        if (Count == 0)
        {
            return;
        }

        if (_items is LythonRuntime.GlobalNamespaceStorage or ObjectNamespaceStorage)
        {
            _items.Clear();
            _version++;
            if (_protocolBytes != 0) _memoryGovernor!.Release(_protocolBytes);
            _sideIndex?.ReleaseAll(_memoryGovernor!);
            _sideIndex = null;
            _protocol = null;
            _protocolBytes = 0;
            return;
        }

        _items.Clear();
        _version++;
        if (_protocolBytes != 0) _memoryGovernor!.Release(_protocolBytes);
        _sideIndex?.ReleaseAll(_memoryGovernor!);
        _sideIndex = null;
        _protocol = null;
        _protocolBytes = 0;
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => Keys;

    // Builds a reverse-keys iterator over a governed snapshot: the snapshot
    // rides a transient reservation for its peak scratch while the retained
    // backing stays on the shared per-run allowance like merged ChainMap
    // iteration scratch. Size changes fail with the shared dictionary text.
    public PyIteratorBase CreateReversedKeysIterator(MemoryGovernor? governor, LythonSourceSpan? span)
    {
        using var scratch = governor?.ReserveTemporary(checked(16L * Count), span);
        return new ReversedKeysIterator(this, Keys.ToArray(), _version);
    }

    private sealed class ReversedKeysIterator : PyIteratorBase
    {
        private readonly PyDict _owner;
        private readonly object[] _snapshot;
        private readonly int _expectedVersion;
        private int _nextIndex;

        public ReversedKeysIterator(PyDict owner, object[] snapshot, int expectedVersion)
        {
            _owner = owner;
            _snapshot = snapshot;
            _expectedVersion = expectedVersion;
            _nextIndex = snapshot.Length - 1;
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            _owner.EnsureUnmodified(_expectedVersion);
            if (_nextIndex < 0)
            {
                value = PyNone.Instance;
                return false;
            }

            value = _snapshot[_nextIndex];
            _nextIndex--;
            return true;
        }

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<reversed object>");
        }
    }
    // Builds a reverse-items iterator over a governed snapshot like the keys
    // shape above; tuples materialize per step exactly like forward items
    // iteration, and size changes fail with the same shared text.
    public PyIteratorBase CreateReversedItemsIterator(MemoryGovernor? governor, LythonSourceSpan? span)
    {
        using var scratch = governor?.ReserveTemporary(checked(32L * Count), span);
        return new ReversedItemsIterator(this, Items.ToArray(), _version);
    }

    private sealed class ReversedItemsIterator : PyIteratorBase
    {
        private readonly PyDict _owner;
        private readonly KeyValuePair<object, object>[] _snapshot;
        private readonly int _expectedVersion;
        private int _nextIndex;

        public ReversedItemsIterator(PyDict owner, KeyValuePair<object, object>[] snapshot, int expectedVersion)
        {
            _owner = owner;
            _snapshot = snapshot;
            _expectedVersion = expectedVersion;
            _nextIndex = snapshot.Length - 1;
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            _owner.EnsureUnmodified(_expectedVersion);
            if (_nextIndex < 0)
            {
                value = PyNone.Instance;
                return false;
            }

            var pair = _snapshot[_nextIndex];
            _nextIndex--;
            var governor = _owner.OwnerMemoryGovernor;
            value = governor is null
                ? PyTuple.FromOwnedArray([pair.Key, pair.Value])
                : PyTuple.FromOwnedArray([pair.Key, pair.Value], governor, _owner.AllocationSpan);
            return true;
        }

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<reversed object>");
        }
    }

    // Builds a reverse-values iterator over a governed snapshot like the
    // keys shape above; size changes fail with the same shared text.
    public PyIteratorBase CreateReversedValuesIterator(MemoryGovernor? governor, LythonSourceSpan? span)
    {
        using var scratch = governor?.ReserveTemporary(checked(16L * Count), span);
        return new ReversedValuesIterator(this, Values.ToArray(), _version);
    }

    private sealed class ReversedValuesIterator : PyIteratorBase
    {
        private readonly PyDict _owner;
        private readonly object[] _snapshot;
        private readonly int _expectedVersion;
        private int _nextIndex;

        public ReversedValuesIterator(PyDict owner, object[] snapshot, int expectedVersion)
        {
            _owner = owner;
            _snapshot = snapshot;
            _expectedVersion = expectedVersion;
            _nextIndex = snapshot.Length - 1;
        }

        public override bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            _owner.EnsureUnmodified(_expectedVersion);
            if (_nextIndex < 0)
            {
                value = PyNone.Instance;
                return false;
            }

            value = _snapshot[_nextIndex];
            _nextIndex--;
            return true;
        }

        public override PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<reversed object>");
        }
    }

    public PyString RenderPython(PyRenderingContext context) => PyRendering.ToReprPyString(this, context);

    public PyString RenderInterpolated(PyRenderingContext context) => PyRendering.ToReprPyString(this, context);

    public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
        => EnumerateItems(_version).GetEnumerator();

    private IEnumerable<KeyValuePair<object, object>> EnumerateItems(int expectedVersion)
    {
        using var enumerator = _items.Entries.GetEnumerator();
        while (true)
        {
            EnsureUnmodified(expectedVersion);
            if (!enumerator.MoveNext())
            {
                yield break;
            }

            var pair = enumerator.Current;
            yield return new KeyValuePair<object, object>(pair.Key, pair.Value);
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    // Replacement uses already funded capacity. If a callback removed the
    // resolved entry, admit any required table growth before inserting again.
    // Callers resolve the
    // stored key first; no version bump (size unchanged, like CPython).
    private void StoreItem(object storageKey, object storageValue)
    {
        _memoryGovernor?.ThrowIfAdmissionFailed();
        var countBefore = Count;
        try
        {
            if (_nativeItems is not null) { _ = SetNativeItem(storageKey, storageValue); return; }
            // Replacement retains the original equal key and changes no size.
            // Only a missing entry requires capacity admission and insertion.
            if (_items.TrySetExisting(storageKey, storageValue)) return;
            _items.AddNew(storageKey, storageValue);
        }
        finally
        {
            // An adapter may publish before throwing. Preserve the visible
            // entry and invalidate size-sensitive iterators in that case too.
            if (Count > countBefore)
            {
                _version++;
            }
        }
    }

    // Only fresh private construction storage; extracted aliases must never use this.
    internal void DiscardUnpublishedStorage()
    {
        RefundAbortedConstruction();
        _items = null!;
        _nativeItems = null;
        _protocol = null;
    }

    private void RefundAbortedConstruction()
    {
        var release = CommittedStorageBytes;
        // Reject a live view before refunding any of its retained backing.
        _ = ReleaseNativeStorage();
        if (_memoryGovernor is not null && release > 0)
        {
            _memoryGovernor.Release(release);
        }
        _protocolBytes = 0;
        _sideIndex = null;
    }

    private void EnsureUnmodified(int expectedVersion)
    {
        if (_version != expectedVersion)
        {
            throw new LythonRuntimeException("RuntimeError", "dictionary changed size during iteration", _allocationSpan);
        }
    }
}
