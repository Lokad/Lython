using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue, IChainMapSource, IPyOwnershipSnapshot
{
    private IPyDictStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private int _version;
    private AdoptedScalarCoupons? _scalarCoupons;

    // Protocol-key side table (R13b): keys needing __hash__/__eq__ dispatch
    // bypass Dictionary probing (which cannot run guest code) and scan with
    // precomputed hashes plus element ==. The fast store keeps every entry;
    // the side list only shadows protocol keys (insertion sequence, hash and
    // key) for order-preserving scans, so iteration order stays exact and
    // later ops stay unified. (N10 part 1: snapshots preflighted, scans
    // budgeted; hash indexing the side table is the part-2 redesign.)
    private List<ProtocolEntry>? _protocol;
    private long _protocolBytes;
    private long _protocolSeq;

    // StructuralHash is frozen at insert: the comparer never dispatches guest
    // code, so it cannot change under a key object and scans never recompute it.
    // Layout stays 24 bytes (long + int + int + reference), covered by the
    // existing per-entry rate.
    internal readonly record struct ProtocolEntry(long Seq, int Hash, int StructuralHash, object Key);

    internal const long ProtocolEntryBytes = 64;

    public PyDict()
    {
        _items = PyDictStorage.Create();
    }

    public PyDict(MemoryGovernor governor) : this(governor, null) { }

    public PyDict(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyDictStorage.Create(0, governor, allocationSpan);
    }

    public PyDict(PyDict other)
    {
        _memoryGovernor = other._memoryGovernor;
        _allocationSpan = other._allocationSpan;
        _items = other._memoryGovernor is null
            ? other._items.Clone()
            : PyDictStorage.Create(other._items, other._memoryGovernor, other._allocationSpan);
        _protocol = other._protocol is null ? null : new List<ProtocolEntry>(other._protocol);
        _protocolBytes = 0;
        _protocolSeq = other._protocolSeq;
        if (_memoryGovernor is not null)
        {
            AdoptInitialItems();
        }
    }

    public PyDict(PyDict other, MemoryGovernor governor) : this(other, governor, null) { }

    public PyDict(PyDict other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyDictStorage.Create(other._items, governor, allocationSpan);
        _protocol = other._protocol is null ? null : new List<ProtocolEntry>(other._protocol);
        _protocolSeq = other._protocolSeq;
        if (other._protocolBytes > 0)
        {
            governor.Reserve(other._protocolBytes, allocationSpan);
            governor.Commit(other._protocolBytes);
            _protocolBytes = other._protocolBytes;
        }

        AdoptInitialItems();
    }

    // The side index holds shadows of fast-store entries, never additional
    // entries, so only the fast store counts. Its backing bytes still add
    // to CommittedStorageBytes below.
    public int Count => _items.Count;

    object IChainMapSource.Underlying => this;

    // Current committed backing charges, for pooled owners that release them
    // if this dictionary is dropped without wholesale storage replacement.
    // Adopted scalar coupons fold in, so snapshots and drop sweeps carry them.
    internal long CommittedStorageBytes => _items.CommittedBytes + _protocolBytes + (_scalarCoupons?.CommittedBytes ?? 0);

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public IEnumerable<object> Keys
    {
        get
        {
            var expectedVersion = _version;
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
    }

    public IEnumerable<object> Values
    {
        get
        {
            var expectedVersion = _version;
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

        if (_protocol is not null && TryGetProtocolBuiltin(key, out value))
        {
            return true;
        }

        value = PyNone.Instance;
        return false;
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
        if (_items.TryGetValue(storageKey, out var current))
        {
            // The original key object stays retained: only the value turns over.
            // Like CPython, replacement changes no size, so no version bump.
            AdoptIncoming(storageValue);
            _ = _items.SetItem(storageKey, storageValue);
            ReleaseOutgoing(FromStorageValue(current));
            NoteGrowth(committedBefore);
            return;
        }

        AdoptIncoming(storageKey);
        AdoptIncoming(storageValue);
        try
        {
            _items = PyDictStorage.EnsureCapacity(_items, Count + 1, _memoryGovernor, _allocationSpan);
        }
        catch (Exception)
        {
            UnadoptIncoming(storageKey);
            UnadoptIncoming(storageValue);
            throw;
        }

        _items.AddNew(storageKey, storageValue);
        _version++;
        NoteGrowth(committedBefore);
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor ??= governor;
        _allocationSpan ??= allocationSpan;
    }

    public bool Remove(object key)
    {
        var committedBefore = CommittedStorageBytes;
        if (_items.TryGetValue(ToStorageKey(key), out var storedValue))
        {
            _ = _items.Remove(ToStorageKey(key));
            RemoveProtocolShadow(key);
            // A structural-twin argument was never adopted (only retained keys
            // earn coupons), so releasing by argument is exact for same-reference
            // removals and a safe no-op otherwise; the stored value always is.
            ReleaseOutgoing(key);
            ReleaseOutgoing(FromStorageValue(storedValue));
            _version++;
            NoteGrowth(committedBefore);
            return true;
        }

        if (RemoveProtocolItem(key))
        {
            _version++;
            return true;
        }

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
        // N10 part 2: scans copy lazily. Hash prechecks (and reference
        // identity) run guest-free over the live tables, so no copy exists
        // until the first candidate that could dispatch guest ==. The copy
        // then reflects pre-dispatch state exactly like an eager snapshot,
        // and scanning continues from the hit with the work budget carried
        // over. Denial points and check cadence are unchanged from part 1.
        var work = 0;
        if (_protocol is not null)
        {
            EnsureSideSnapshot(_protocol.Count, useSpan);
            var sideHit = -1;
            for (var i = 0; i < _protocol.Count; i++)
            {
                if ((++work & 63) == 0)
                {
                    context.CheckExecutionBudget(useSpan);
                }

                var entry = _protocol[i];
                if (entry.Hash == hash || entry.StructuralHash == structuralHash)
                {
                    sideHit = i;
                    break;
                }
            }

            if (sideHit >= 0)
            {
                var snapshot = _protocol.ToArray();
                for (var i = sideHit; i < snapshot.Length; i++)
                {
                    // The hit entry already consumed its budget check above.
                    if (i > sideHit && (++work & 63) == 0)
                    {
                        context.CheckExecutionBudget(useSpan);
                    }

                    var entry = snapshot[i];
                    if ((entry.Hash == hash || entry.StructuralHash == structuralHash) && (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan)))
                    {
                        if (_items.TryGetValue(ToStorageKey(entry.Key), out var stored))
                        {
                            value = FromStorageValue(stored);
                            return true;
                        }
                    }
                }
            }
        }

        EnsureStoreSnapshot(_items.Count, useSpan);
        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecutionBudget(useSpan);
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
            var storeSnapshot = _items.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                // The hit entry already consumed its budget check above.
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecutionBudget(useSpan);
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
    private bool TryGetProtocolBuiltin(object key, [MaybeNullWhen(false)] out object value)
    {
        if (_protocol is null)
        {
            value = PyNone.Instance;
            return false;
        }

        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        var hash = PyValueComparer.Instance.GetHashCode(key);
        // N10 part 2: the side list snapshots lazily like every other scan.
        // Dispatching guest == over the live list crashes when the callback
        // mutates the table mid-scan.
        var work = 0;
        var hit = -1;
        for (var i = 0; i < _protocol.Count; i++)
        {
            // N10 part 1: budget non-collision scans too; the context may be
            // null on context-free paths (identity fast path only then).
            if ((++work & 63) == 0 && context is not null && useSpan is not null)
            {
                context.CheckExecutionBudget(useSpan);
            }

            if (_protocol[i].Hash == hash)
            {
                hit = i;
                break;
            }
        }

        if (hit >= 0)
        {
            EnsureSideSnapshot(_protocol.Count, useSpan);
            var snapshot = _protocol.ToArray();
            for (var i = hit; i < snapshot.Length; i++)
            {
                if (i > hit && (++work & 63) == 0 && context is not null && useSpan is not null)
                {
                    context.CheckExecutionBudget(useSpan);
                }

                var entry = snapshot[i];
                if (entry.Hash != hash)
                {
                    continue;
                }

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
        }

        value = PyNone.Instance;
        return false;
    }

    // Ensures the side index exists; entries are charged per slot below.
    private void EnsureSideTable()
    {
        _protocol ??= new List<ProtocolEntry>();
    }

    // N10 part 1: side-table and store snapshots deny before they can
    // allocate. Sizes follow the tracked entry rate and the 32+16 array
    // convention; unowned dictionaries skip silently like all other paths.
    private void EnsureSideSnapshot(int count, LythonSourceSpan? span)
        => _memoryGovernor?.EnsureCanReserve(checked(ProtocolEntryBytes * (long)count), span ?? _allocationSpan);

    private void EnsureStoreSnapshot(int count, LythonSourceSpan? span)
        => _memoryGovernor?.EnsureCanReserve(checked(32L + (16L * count)), span ?? _allocationSpan);

    private void CommitSideBytes(long bytes)
    {
        if (_memoryGovernor is null || bytes <= 0)
        {
            return;
        }

        _memoryGovernor.Reserve(bytes, _allocationSpan);
        _memoryGovernor.Commit(bytes);
        _protocolBytes += bytes;
        ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
    }

    private void ReleaseSideBytes(long bytes)
    {
        if (_memoryGovernor is null || bytes <= 0)
        {
            return;
        }

        _memoryGovernor.Release(bytes);
        _protocolBytes -= bytes;
        ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
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
        // N10 part 1: bound collision and non-collision scans alike.
        // N10 part 2: the snapshots below are lazy (see TryGetProtocolValueExplicit).
        var work = 0;
        if (_protocol is not null)
        {
            EnsureSideSnapshot(_protocol.Count, useSpan);
            var sideHit = -1;
            for (var i = 0; i < _protocol.Count; i++)
            {
                if ((++work & 63) == 0)
                {
                    context.CheckExecutionBudget(useSpan);
                }

                var entry = _protocol[i];
                if (entry.Hash == hash || entry.StructuralHash == structuralHash)
                {
                    sideHit = i;
                    break;
                }
            }

            if (sideHit >= 0)
            {
                var snapshot = _protocol.ToArray();
                for (var i = sideHit; i < snapshot.Length; i++)
                {
                    if (i > sideHit && (++work & 63) == 0)
                    {
                        context.CheckExecutionBudget(useSpan);
                    }

                    var entry = snapshot[i];
                    if ((entry.Hash == hash || entry.StructuralHash == structuralHash) && (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan)))
                    {
                        ReplaceStoredValue(ToStorageKey(entry.Key), storageValue);
                        return;
                    }
                }
            }
        }

        EnsureStoreSnapshot(_items.Count, useSpan);
        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecutionBudget(useSpan);
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
            var storeSnapshot = _items.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecutionBudget(useSpan);
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
        AdoptIncoming(storageKey);
        AdoptIncoming(storageValue);
        try
        {
            _items = PyDictStorage.EnsureCapacity(_items, checked(_items.Count + 1), _memoryGovernor, _allocationSpan);
        }
        catch (Exception)
        {
            UnadoptIncoming(storageKey);
            UnadoptIncoming(storageValue);
            throw;
        }

        _items.AddNew(storageKey, storageValue);
        EnsureSideTable();
        _protocol!.Add(new ProtocolEntry(_protocolSeq++, hash, structuralHash, storageKey));
        _version++;
        if (_memoryGovernor is not null)
        {
            _memoryGovernor.Reserve(ProtocolEntryBytes, _allocationSpan);
            _memoryGovernor.Commit(ProtocolEntryBytes);
            _protocolBytes += ProtocolEntryBytes;
        }

        // A denied protocol charge needs no extra rollback: the stranded entry
        // stays retained, exactly what the coupons cover.
        NoteGrowth(committedBefore);
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
        // N10 part 1: bound collision and non-collision scans alike.
        // N10 part 2: the snapshots below are lazy (see TryGetProtocolValueExplicit).
        var work = 0;
        if (_protocol is not null)
        {
            EnsureSideSnapshot(_protocol.Count, useSpan);
            var sideHit = -1;
            for (var i = 0; i < _protocol.Count; i++)
            {
                if ((++work & 63) == 0)
                {
                    context.CheckExecutionBudget(useSpan);
                }

                var entry = _protocol[i];
                if (entry.Hash == hash || entry.StructuralHash == structuralHash)
                {
                    sideHit = i;
                    break;
                }
            }

            if (sideHit >= 0)
            {
                var snapshot = _protocol.ToArray();
                for (var i = sideHit; i < snapshot.Length; i++)
                {
                    if (i > sideHit && (++work & 63) == 0)
                    {
                        context.CheckExecutionBudget(useSpan);
                    }

                    var entry = snapshot[i];
                    if ((entry.Hash == hash || entry.StructuralHash == structuralHash) && (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan)))
                    {
                        RemoveSideEntry(entry.Key);
                        return true;
                    }
                }
            }
        }

        EnsureStoreSnapshot(_items.Count, useSpan);
        var storeHit = -1;
        var probe = 0;
        foreach (var pair in _items)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecutionBudget(useSpan);
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
            var storeSnapshot = _items.ToArray();
            for (var i = storeHit; i < storeSnapshot.Length; i++)
            {
                if (i > storeHit && (++work & 63) == 0)
                {
                    context.CheckExecutionBudget(useSpan);
                }

                var pair = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(pair.Key) == hash || PyValueComparer.Instance.GetHashCode(pair.Key) == structuralHash) && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(FromStorageKey(pair.Key), key, context, useSpan)))
                {
                    var committedBefore = CommittedStorageBytes;
                    _items.Remove(ToStorageKey(pair.Key));
                    RemoveProtocolShadow(pair.Key);
                    ReleaseOutgoing(FromStorageKey(pair.Key));
                    ReleaseOutgoing(FromStorageValue(pair.Value));
                    NoteGrowth(committedBefore);
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
                _protocol.RemoveAt(i);
                ReleaseSideBytes(ProtocolEntryBytes);
                return;
            }
        }
    }

    private void RemoveSideEntry(object key)
    {
        // Callers always pass the stored entry key (side-scan and structural
        // matches resolve it first), so the released coupons are the evicted pair.
        if (_protocol is not null)
        {
            for (var i = 0; i < _protocol.Count; i++)
            {
                if (ReferenceEquals(_protocol[i].Key, key))
                {
                    _ = _items.TryGetValue(ToStorageKey(_protocol[i].Key), out var storedValue);
                    ReleaseOutgoing(FromStorageKey(_protocol[i].Key));
                    ReleaseOutgoing(storedValue);
                    _protocol.RemoveAt(i);
                    break;
                }
            }
        }

        _items.Remove(ToStorageKey(key));
        ReleaseSideBytes(ProtocolEntryBytes);
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


        _version++;
        if (_memoryGovernor is not null)
        {
            var released = _items.ReleaseCommittedBytes() + _protocolBytes;
            if (released > 0)
            {
                _memoryGovernor.Release(released);
            }

            ReleaseAllCoupons();
            _items = PyDictStorage.Create(0, _memoryGovernor, _allocationSpan);
            _protocol = null;
            _protocolBytes = 0;
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
            return;
        }

        _items.Clear();
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
    {
        var expectedVersion = _version;
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

    // Replaces the value held under an already-stored key: adopts the incoming
    // value before swapping (a denial keeps the old value), then releases the
    // displaced one. The stored key object never turns over. Callers resolve the
    // stored key first; no version bump (size unchanged, like CPython).
    private void ReplaceStoredValue(object storageKey, object storageValue)
    {
        var committedBefore = CommittedStorageBytes;
        _ = _items.TryGetValue(storageKey, out var prior);
        AdoptIncoming(storageValue);
        _ = _items.SetItem(storageKey, storageValue);
        ReleaseOutgoing(prior);
        NoteGrowth(committedBefore);
    }

    // Refreshes the tracked snapshot when the owned total moved (storage or
    // coupons): a stale snapshot would over-release on a later drop sweep.
    private void NoteGrowth(long committedBefore)
    {
        if (CommittedStorageBytes != committedBefore)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }
    }

    // Adopts construction contents with a refund of the orphaned charges when
    // a coupon denies: callers publish nothing on failure. AdoptAllPairs rolls
    // its own coupons back, so only backing and protocol refund here.
    private void AdoptInitialItems()
    {
        var incoming = new AdoptedScalarCoupons();
        try
        {
            incoming.AdoptAllPairs(_items, _memoryGovernor!, _allocationSpan);
        }
        catch
        {
            RefundAbortedConstruction();
            throw;
        }

        _scalarCoupons = incoming.CommittedBytes > 0 ? incoming : null;
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
        _scalarCoupons = null;
    }

    // Adopts one incoming value when governed; denies before the caller mutates.
    private void AdoptIncoming(object value)
    {
        if (_memoryGovernor is null || !AdoptedScalarCoupons.IsAdoptableScalar(value))
        {
            return;
        }

        _scalarCoupons ??= new AdoptedScalarCoupons();
        _scalarCoupons.Adopt(value, _memoryGovernor, _allocationSpan);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    private void UnadoptIncoming(object value)
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            return;
        }

        _scalarCoupons.Release(value, _memoryGovernor);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }
    }

    // Releases one outgoing reference and refreshes the snapshot when the owned
    // total moved, so drops never sweep a stale charge.
    private void ReleaseOutgoing(object? value)
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            return;
        }

        var committedBefore = CommittedStorageBytes;
        _scalarCoupons.Release(value, _memoryGovernor);
        if (_scalarCoupons.CommittedBytes == 0)
        {
            _scalarCoupons = null;
        }

        NoteGrowth(committedBefore);
    }

    private void ReleaseAllCoupons()
    {
        if (_scalarCoupons is null || _memoryGovernor is null)
        {
            _scalarCoupons = null;
            return;
        }

        var committedBefore = CommittedStorageBytes;
        _scalarCoupons.ReleaseAll(_memoryGovernor);
        _scalarCoupons = null;
        NoteGrowth(committedBefore);
    }

    private void EnsureUnmodified(int expectedVersion)
    {
        if (_version != expectedVersion)
        {
            throw new LythonRuntimeException("RuntimeError", "dictionary changed size during iteration", _allocationSpan);
        }
    }
}
