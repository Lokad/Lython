using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue, IChainMapSource, IPyOwnershipSnapshot
{
    private IPyDictStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private int _version;
    private long _entryStorageBytes;
    private const long EntryStorageBytes = 128;
    // Wrapped storage keeps its charged owner alive through escaped views and
    // iterators. All storage mutations refresh that one existing snapshot.

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

    public PyDict()
    {
        _items = PyDictStorage.Create();
    }

    public PyDict(MemoryGovernor governor) : this(governor, null) { }

    internal PyDict(IPyDictStorage storage, MemoryGovernor governor, LythonSourceSpan allocationSpan)
    {
        _items = storage;
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
    }

    public PyDict(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyDictStorage.Create(0, governor, allocationSpan);
    }

    public PyDict(PyDict other) : this(other, other._memoryGovernor, other._allocationSpan) { }

    public PyDict(PyDict other, MemoryGovernor governor) : this(other, governor, null) { }

    public PyDict(PyDict other, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyDictStorage.Create(other._items, governor, allocationSpan);
        _protocolSeq = other._protocolSeq;
        try
        {
            ChargeInitialEntries();
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
    // Every retained pair carries the same small-value headroom. Larger payloads
    // keep their separate owners; aliases never require identity bookkeeping.
    internal long CommittedStorageBytes => _items.CommittedBytes + _protocolBytes + (_sideIndex?.CommittedBytes ?? 0) + _entryStorageBytes;

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
        using var enumerator = _items.GetEnumerator();
        while (true)
        {
            EnsureUnmodified(expectedVersion);
            if (!enumerator.MoveNext())
            {
                yield break;
            }

            yield return FromStorageKey(enumerator.Current.Key);
        }
    }

    public IEnumerable<object> Values
    {
        get => EnumerateValues(_version);
    }

    private IEnumerable<object> EnumerateValues(int expectedVersion)
    {
        using var enumerator = _items.GetEnumerator();
        while (true)
        {
            EnsureUnmodified(expectedVersion);
            if (!enumerator.MoveNext())
            {
                yield break;
            }

            yield return FromStorageValue(enumerator.Current.Value);
        }
    }

    public IEnumerable<KeyValuePair<object, object>> Items => this;

    public object GetItem(object key) => FromStorageValue(_items.GetRequired(ToStorageKey(key)));

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value)
    {
        if (_items.TryGetValue(ToStorageKey(key), out var stored))
        {
            value = FromStorageValue(stored);
            return true;
        }

        if (PyHashProtocols.NeedsProtocolKey(key))
        {
            return TryGetProtocolValue(key, out value);
        }

        if (_protocol is not null && TryGetProtocolBuiltin(key, out value))
        {
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    // Hop-proof lookup for async == (ambient ThreadStatics do not survive
    // thread hops between awaits); synchronous paths rely on the ambient
    // branch inside TryGetProtocolValue instead.
    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_items.TryGetValue(ToStorageKey(key), out var stored))
        {
            value = FromStorageValue(stored);
            return true;
        }

        if (PyHashProtocols.NeedsProtocolKey(key))
        {
            return TryGetProtocolValueExplicit(key, context, span, out value);
        }

        if (_protocol is not null && TryGetProtocolBuiltin(key, out value, context, span))
        {
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    // Keyword defaults and dict.get use builtin string keys. Table probing itself
    // never invokes guest code; only a protocol-key collision can suspend.
    internal ValueTask<(bool Found, object Value)> TryGetBuiltinStringAsync(
        PyString key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_items.TryGetValue(key, out var stored)) return new((true, FromStorageValue(stored)));
        if (!HasProtocolKeys) return new((false, PyNone.Instance));
        return LookupAsync();

        async ValueTask<(bool Found, object Value)> LookupAsync()
        {
            var hash = PyValueComparer.Instance.GetHashCode(key);
            using var candidatesStorage = _memoryGovernor?.ReserveTemporary(
                ProtocolSideIndex.EstimateSnapshotBytes(_protocol?.Count ?? 0, ProtocolEntryBytes), span);
            var candidates = ProtocolSideIndex.SnapshotCandidates(_protocol!, _sideIndex, hash, hash, builtinOnly: true, span, context);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (((i + 1) & 63) == 0) context.CheckExecution(span);
                var entry = candidates[i];
                if ((ReferenceEquals(entry.Key, key) ||
                    await LythonRuntime.ElementEqualsAsync(entry.Key, key, context, span).ConfigureAwait(false)) &&
                    _items.TryGetValue(ToStorageKey(entry.Key), out var value))
                    return (true, FromStorageValue(value));
            }
            return (false, PyNone.Instance);
        }
    }

    public bool ContainsKey(object key)
    {
        if (_items.ContainsKey(ToStorageKey(key)))
        {
            return true;
        }

        if (PyHashProtocols.NeedsProtocolKey(key))
        {
            return TryGetProtocolValue(key, out _);
        }

        return _protocol is not null && TryGetProtocolBuiltin(key, out _);
    }

    public void SetItem(object key, object value)
    {
        if (PyHashProtocols.NeedsProtocolKey(key))
        {
            SetProtocolItem(key, value);
            return;
        }

        var storageKey = ToStorageKey(key);
        var storageValue = ToStorageValue(value);
        var committedBefore = CommittedStorageBytes;
        if (_items.ContainsKey(storageKey))
        {
            // The original key object stays retained: only the value turns over.
            // Like CPython, replacement changes no size, so no version bump.
            ReplaceStoredValue(storageKey, storageValue);
            return;
        }

        var entryBytes = _memoryGovernor is null ? 0 : EntryStorageBytes;
        _memoryGovernor?.Reserve(entryBytes, _allocationSpan);
        var countBefore = Count;
        try
        {
            _items = PyDictStorage.EnsureCapacity(_items, Count + 1, _memoryGovernor, _allocationSpan);
            _items.AddNew(storageKey, storageValue);
        }
        finally
        {
            // A storage adapter may publish before throwing. Preserve that
            // visible entry and its funding; refusal before publication refunds.
            if (Count > countBefore)
            {
                _memoryGovernor?.Commit(entryBytes);
                _entryStorageBytes += entryBytes;
                _version++;
            }
            else _memoryGovernor?.ReleaseReserved(entryBytes);
        }
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
        IPyDictStorage? storage = null;
        ProtocolSideIndex? sideIndex = null;
        var entryBytes = checked((long)Count * EntryStorageBytes);
        var protocolBytes = checked((long)(_protocol?.Count ?? 0) * ProtocolEntryBytes);
        var valueBytes = checked(entryBytes + protocolBytes);
        governor.Reserve(valueBytes, span);
        try
        {
            using var scratch = governor.ReserveTemporary(checked(16L * Count), span);
            storage = PyDictStorage.Create(_items, governor, span);
            if (_sideIndex is not null)
            {
                sideIndex = new ProtocolSideIndex();
                sideIndex.CloneFrom(_sideIndex, governor, span);
            }
        }
        catch
        {
            governor.ReleaseReserved(valueBytes);
            if (storage is not null) governor.Release(storage.ReleaseCommittedBytes());
            sideIndex?.ReleaseAll(governor);
            throw;
        }
        governor.Commit(valueBytes);
        _items = storage;
        _sideIndex = sideIndex;
        _entryStorageBytes = entryBytes;
        _protocolBytes = protocolBytes;
        _memoryGovernor = governor;
        _allocationSpan = span;
    }

    public bool Remove(object key)
    {
        var committedBefore = CommittedStorageBytes;
        if (_items.TryGetValue(ToStorageKey(key), out var storedValue))
        {
            _ = _items.Remove(ToStorageKey(key));
            RemoveProtocolShadow(key);
            ReleaseEntryStorage();
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
        // Fast store probe first, like TryGetValue: same-object hits never
        // dispatch guest code.
        if (_items.TryGetValue(ToStorageKey(key), out var stored))
        {
            value = FromStorageValue(stored);
            RemoveResolvedEntry(ToStorageKey(key), key, value);
            return true;
        }

        if (PyHashProtocols.NeedsProtocolKey(key))
        {
            return TryRemoveProtocolValue(key, out value);
        }

        if (_protocol is not null && TryRemoveProtocolBuiltin(key, out value))
        {
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    // Deletes an already-resolved stored entry: no guest dispatch, version
    // bumped exactly once, pool snapshots refreshed. Entry funding is independent
    // of the identity or type of the lookup argument.
    private void RemoveResolvedEntry(object storageKey, object releaseKey, object storedValue)
    {
        var committedBefore = CommittedStorageBytes;
        _ = _items.Remove(storageKey);
        RemoveProtocolShadow(releaseKey);
        ReleaseEntryStorage();
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
                    if (_items.TryGetValue(ToStorageKey(entry.Key), out var stored))
                    {
                        value = FromStorageValue(stored);
                        RemoveResolvedEntry(ToStorageKey(entry.Key), entry.Key, value);
                        return true;
                    }
                }
            }
        }

        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items)
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
            var storeSnapshot = _items.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                // The hit entry already consumed its budget check above.
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var pair = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash) && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(FromStorageKey(pair.Key), key, context, useSpan)))
                {
                    if (_items.TryGetValue(ToStorageKey(pair.Key), out var stored))
                    {
                        value = FromStorageValue(stored);
                        RemoveResolvedEntry(ToStorageKey(pair.Key), pair.Key, value);
                        return true;
                    }
                }
            }
        }

        value = PyNone.Instance;
        return false;
    }

    private bool TryRemoveProtocolBuiltin(object key, [MaybeNullWhen(false)] out object value)
    {
        if (_protocol is null)
        {
            value = PyNone.Instance;
            return false;
        }

        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        var hash = PyValueComparer.Instance.GetHashCode(key);
        var work = 0;
        using var candidatesStorage = _memoryGovernor?.ReserveTemporary(
            ProtocolSideIndex.EstimateSnapshotBytes(_protocol?.Count ?? 0, ProtocolEntryBytes), useSpan);
        var candidates = ProtocolSideIndex.SnapshotCandidates(_protocol, _sideIndex, hash, hash, builtinOnly: true, useSpan, context);
        for (var i = 0; i < candidates.Length; i++)
        {
            // N10 part 1: budget non-collision scans too; the context may be
            // null on context-free paths (identity fast path only then).
            if ((++work & 63) == 0 && context is not null && useSpan is not null)
            {
                context.CheckExecution(useSpan);
            }

            var entry = candidates[i];
            var match = ReferenceEquals(entry.Key, key);
            if (!match && context is not null && useSpan is not null)
            {
                match = LythonRuntime.ElementEquals(entry.Key, key, context, useSpan);
            }

            if (match && _items.TryGetValue(ToStorageKey(entry.Key), out var stored))
            {
                value = FromStorageValue(stored);
                RemoveResolvedEntry(ToStorageKey(entry.Key), entry.Key, value);
                return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    private bool TryRemoveProtocolStructural(object key, [MaybeNullWhen(false)] out object value)
    {
        if (_protocol is not null)
        {
            for (var i = 0; i < _protocol.Count; i++)
            {
                var entry = _protocol[i];
                if (ReferenceEquals(entry.Key, key) && _items.TryGetValue(ToStorageKey(entry.Key), out var stored))
                {
                    value = FromStorageValue(stored);
                    RemoveResolvedEntry(ToStorageKey(entry.Key), entry.Key, value);
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
                    if (_items.TryGetValue(ToStorageKey(entry.Key), out var stored))
                    {
                        value = FromStorageValue(stored);
                        return true;
                    }
                }
            }
        }

        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items)
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
            var storeSnapshot = _items.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                // The hit entry already consumed its budget check above.
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var pair = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash) && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(FromStorageKey(pair.Key), key, context, useSpan)))
                {
                    value = FromStorageValue(pair.Value);
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
                if (ReferenceEquals(entry.Key, key) && _items.TryGetValue(ToStorageKey(entry.Key), out var stored))
                {
                    value = FromStorageValue(stored);
                    return true;
                }
            }
        }

        value = PyNone.Instance;
        return false;
    }

    // Builtin-key lookup against side-homed protocol entries: comparer hash
    // plus identity always, element == when a context is active.
    private bool TryGetProtocolBuiltin(object key, [MaybeNullWhen(false)] out object value,
        LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? useSpan = null)
    {
        if (_protocol is null)
        {
            value = PyNone.Instance;
            return false;
        }

        context ??= ActiveProtocolContext();
        useSpan ??= PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        var hash = PyValueComparer.Instance.GetHashCode(key);
        // N10 index: builtin probes scan only hash-bucketed candidates in insertion order
        // (same dispatch set: entry protocol hash only). Snapshotting the candidates keeps
        // mutating guest == safe like every other scan.
        var work = 0;
        using var candidatesStorage = _memoryGovernor?.ReserveTemporary(
            ProtocolSideIndex.EstimateSnapshotBytes(_protocol?.Count ?? 0, ProtocolEntryBytes), useSpan);
        var candidates = ProtocolSideIndex.SnapshotCandidates(_protocol, _sideIndex, hash, hash, builtinOnly: true, useSpan, context);
        for (var i = 0; i < candidates.Length; i++)
        {
            // N10 part 1: budget non-collision scans too; the context may be
            // null on context-free paths (identity fast path only then).
            if ((++work & 63) == 0 && context is not null && useSpan is not null)
            {
                context.CheckExecution(useSpan);
            }

            var entry = candidates[i];
            var match = ReferenceEquals(entry.Key, key);
            if (!match && context is not null && useSpan is not null)
            {
                match = LythonRuntime.ElementEquals(entry.Key, key, context, useSpan);
            }

            if (match && _items.TryGetValue(ToStorageKey(entry.Key), out var stored))
            {
                value = FromStorageValue(stored);
                return true;
            }
        }

        value = PyNone.Instance;
        return false;
    }

    // Ensures the side table and its candidate index exist; entries are charged per slot below.
    // The index stays lazy so ordinary-key paths pay nothing.
    private void EnsureSideTable()
    {
        _protocol ??= new List<ProtocolEntry>();
        _sideIndex ??= new ProtocolSideIndex();
    }

    private void CommitSideBytes(long bytes)
    {
        if (_memoryGovernor is null || bytes <= 0)
        {
            return;
        }

        _memoryGovernor.Reserve(bytes, _allocationSpan);
        _memoryGovernor.Commit(bytes);
        _protocolBytes += bytes;
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
    private void SetProtocolItem(object key, object value)
    {
        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        var storageValue = ToStorageValue(value);
        if (context is null || useSpan is null)
        {
            SetProtocolItemStructural(key, storageValue);
            return;
        }

        var hash = PyHashProtocols.GetProtocolHash(key, context, useSpan);
        var structuralHash = PyValueComparer.Instance.GetHashCode(key);
        // N10 index: update in place over hash-bucketed candidates only (same dispatch
        // set and order); the store phase below is untouched.
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
                    ReplaceStoredValue(ToStorageKey(entry.Key), storageValue);
                    return;
                }
            }
        }

        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items)
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
            var storeSnapshot = _items.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var pair = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash) && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(FromStorageKey(pair.Key), key, context, useSpan)))
                {
                    ReplaceStoredValue(ToStorageKey(pair.Key), storageValue);
                    return;
                }
            }
        }

        AppendProtocolItem(key, storageValue, hash, structuralHash);
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
                    ReplaceStoredValue(ToStorageKey(entry.Key), storageValue);
                    return;
                }
            }
        }

        var structuralHash = PyValueComparer.Instance.GetHashCode(key);
        AppendProtocolItem(key, storageValue, structuralHash, structuralHash);
    }

    private void AppendProtocolItem(object key, object storageValue, int hash, int structuralHash)
    {
        var committedBefore = CommittedStorageBytes;
        var storageKey = ToStorageKey(key);
        var entryBytes = _memoryGovernor is null ? 0 : EntryStorageBytes;
        var protocolBytes = _memoryGovernor is null ? 0 : ProtocolEntryBytes;
        var bytes = entryBytes + protocolBytes;
        _memoryGovernor?.Reserve(bytes, _allocationSpan);
        var countBefore = Count;
        try
        {
            EnsureSideTable();
            var position = _protocol!.Count;
            _sideIndex!.Add(position, hash, structuralHash, _memoryGovernor, _allocationSpan);
            try
            {
                _items = PyDictStorage.EnsureCapacity(_items, checked(Count + 1), _memoryGovernor, _allocationSpan);
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
                _entryStorageBytes += entryBytes;
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
        foreach (var pair in _items)
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
            var storeSnapshot = _items.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecution(useSpan);
                }

                var pair = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash) && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(FromStorageKey(pair.Key), key, context, useSpan)))
                {
                    var committedBefore = CommittedStorageBytes;
                    _items.Remove(ToStorageKey(pair.Key));
                    RemoveProtocolShadow(pair.Key);
                    ReleaseEntryStorage();
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
        // Only a successful deletion can release this entry's storage.
        if (!_items.Remove(ToStorageKey(key))) return;
        ReleaseEntryStorage();
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

        _ = Remove(key);
        return true;
    }

    public void Clear()
    {
        if (Count == 0)
        {
            return;
        }

        if (_items is LythonRuntime.GlobalNamespaceStorage)
        {
            _items.Clear();
            _version++;
            if (_protocolBytes != 0) _memoryGovernor!.Release(_protocolBytes);
            _sideIndex?.ReleaseAll(_memoryGovernor!);
            _sideIndex = null;
            _protocol = null;
            _protocolBytes = 0;
            ReleaseAllEntryStorage();
            return;
        }

        _version++;
        if (_memoryGovernor is not null)
        {
            var released = _items.ReleaseCommittedBytes() + _protocolBytes;
            if (released > 0)
            {
                _memoryGovernor.Release(released);
            }

            _sideIndex?.ReleaseAll(_memoryGovernor);
            _sideIndex = null;
            ReleaseAllEntryStorage();
            _items = PyDictStorage.Create(0, _memoryGovernor, _allocationSpan);
            _protocol = null;
            _protocolBytes = 0;
            return;
        }

        _items.Clear();
        _protocol = null;
        _sideIndex = null;
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
        using var enumerator = _items.GetEnumerator();
        while (true)
        {
            EnsureUnmodified(expectedVersion);
            if (!enumerator.MoveNext())
            {
                yield break;
            }

            var pair = enumerator.Current;
            yield return new KeyValuePair<object, object>(FromStorageKey(pair.Key), FromStorageValue(pair.Value));
        }
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private static object ToStorageKey(object key) => ReferenceEquals(key, PyNone.Instance) ? PyNone.Instance : key;

    private static object FromStorageKey(object key) => key;

    private static object ToStorageValue(object value) => value;

    private static object FromStorageValue(object value) => value;

    // Replacement transfers existing entry funding. The stored key never turns
    // over. If a callback removed the resolved entry, fund its insertion afresh.
    // Callers resolve the
    // stored key first; no version bump (size unchanged, like CPython).
    private void ReplaceStoredValue(object storageKey, object storageValue)
    {
        var committedBefore = CommittedStorageBytes;
        var entryBytes = _memoryGovernor is not null && !_items.ContainsKey(storageKey) ? EntryStorageBytes : 0;
        _memoryGovernor?.Reserve(entryBytes, _allocationSpan);
        var countBefore = Count;
        try
        {
            if (entryBytes > 0)
                _items = PyDictStorage.EnsureCapacity(_items, checked(Count + 1), _memoryGovernor, _allocationSpan);
            _ = _items.SetItem(storageKey, storageValue);
        }
        finally
        {
            if (Count > countBefore)
            {
                _memoryGovernor?.Commit(entryBytes);
                _entryStorageBytes += entryBytes;
                _version++;
            }
            else _memoryGovernor?.ReleaseReserved(entryBytes);
        }
    }

    private void ChargeInitialEntries()
    {
        if (_memoryGovernor is null) return;
        var bytes = checked((long)Count * EntryStorageBytes);
        _memoryGovernor.Reserve(bytes, _allocationSpan);
        _memoryGovernor.Commit(bytes);
        _entryStorageBytes = bytes;
    }

    private void RefundAbortedConstruction()
    {
        if (_memoryGovernor is not null)
        {
            var release = CommittedStorageBytes;
            if (release > 0)
            {
                _memoryGovernor.Release(release);
            }
        }

        _ = _items.ReleaseCommittedBytes();
        _protocolBytes = 0;
        _sideIndex = null;
        _entryStorageBytes = 0;
    }

    private void ReleaseEntryStorage()
    {
        if (_memoryGovernor is null) return;
        _memoryGovernor.Release(EntryStorageBytes);
        _entryStorageBytes -= EntryStorageBytes;
    }

    private void ReleaseAllEntryStorage()
    {
        _memoryGovernor?.Release(_entryStorageBytes);
        _entryStorageBytes = 0;
    }

    private void EnsureUnmodified(int expectedVersion)
    {
        if (_version != expectedVersion)
        {
            throw new LythonRuntimeException("RuntimeError", "dictionary changed size during iteration", _allocationSpan);
        }
    }
}
