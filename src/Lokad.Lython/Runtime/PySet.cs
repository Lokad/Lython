using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PySet : IEnumerable<object>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue, IPyOwnershipSnapshot
{
    private HashSet<object> _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private long _committedBytes;
    private AdoptedScalarCoupons? _scalarCoupons;

    // Protocol-item side index (R13b): items needing __hash__/__eq__ dispatch
    // bypass HashSet probing (which cannot run guest code) and scan with
    // precomputed hashes plus element ==. Dual-homed like PyDict: every
    // entry lives in the fast table (iteration, Count, copies untouched)
    // with a shadow here carrying its contextual hash for dispatch scans.
    // (N10: snapshots preflighted, scans budgeted, side candidates indexed
    // by protocol hash through the shared ProtocolSideIndex.)
    private List<ProtocolEntry>? _protocol;
    private ProtocolSideIndex? _sideIndex;
    private long _protocolBytes;
    private long _protocolSeq;

    // StructuralHash is frozen at insert: the comparer never dispatches guest
    // code, so it cannot change under an item object and scans never recompute it.
    // Layout stays 24 bytes (long + int + int + reference), covered by the
    // existing per-entry rate.
    internal readonly record struct ProtocolEntry(long Seq, int Hash, int StructuralHash, object Item);

    internal const long ProtocolEntryBytes = 64;
    // Committed capacity, not live CLR capacity: it lags behind after a failed
    // growth so the retry re-charges instead of riding enlarged storage for free.
    private int _capacity;

    // MG04: the wrapper plus its empty table object outlive every capacity
    // decision, so each distinct governed set owns one shell charge for its
    // lifetime (at the constructed-function shell rate, covering the measured
    // ~120 omitted bytes per empty set). Backing capacity stays separate:
    // Clear releases it while the shell persists, and regrowth re-charges it.
    private const long SetShellBytes = 128;
    private bool _shellCharged;

    public PySet()
    {
        _items = new HashSet<object>(PyValueComparer.Instance);
    }

    public PySet(MemoryGovernor governor) : this(governor, null) { }

    public PySet(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this()
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        ChargeShell();
    }

    public PySet(IEnumerable<object> items)
        : this()
    {
        foreach (var item in items)
        {
            _items.Add(item);
        }
    }

    public PySet(IEnumerable<object> items, MemoryGovernor governor) : this(items, governor, null) { }

    public PySet(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(governor, allocationSpan)
    {
        // Reserve the table before any copy exists. Sized snapshots then ride
        // a transient reservation while they coexist with the table; the
        // snapshot keeps enumeration robust when reentrant user equality code
        // mutates the source mid-copy.
        if (items is object[] array)
        {
            EnsureCapacity(array.Length);
            foreach (var item in array)
            {
                _items.Add(item);
            }

            AdoptInitialItems();
            return;
        }

        if (items is PySet other)
        {
            EnsureCapacity(other.Count);
            if (other.Count == 0)
            {
                return;
            }

            using var scratch = governor.ReserveTemporary(EstimateSnapshotBytes(other.Count), allocationSpan);
            var materialized = other._items.ToArray();
            foreach (var item in materialized)
            {
                _items.Add(item);
            }

            AdoptInitialItems();
            return;
        }

        if (items is ICollection<object> collection)
        {
            EnsureCapacity(collection.Count);
            if (collection.Count == 0)
            {
                return;
            }

            using var scratch = governor.ReserveTemporary(EstimateSnapshotBytes(collection.Count), allocationSpan);
            var materialized = new object[collection.Count];
            collection.CopyTo(materialized, 0);
            foreach (var item in materialized)
            {
                _items.Add(item);
            }

            AdoptInitialItems();
            return;
        }

        // Lazy source: no count exists to pre-size from, so Add governs each
        // growth step with the committed-capacity atomicity above instead of
        // draining the whole source into an uncharged array first. Add adopts
        // each insertion, so no bulk pass is needed here.
        foreach (var item in items)
        {
            _ = Add(item);
        }
    }

    private static long EstimateSnapshotBytes(int count) => 24L + (8L * count);

    // Filter scratch holds only references, so an empty bound allocates
    // nothing and reserves nothing: filtering an empty set stays free even
    // under a zero budget.
    private static long EstimateFilterBytes(int count) => count == 0 ? 0L : 24L + (8L * count);

    // Owns the shell once: construction and first-attach are the only paths
    // that introduce a governed set, so the flag makes each distinct object
    // pay exactly once while aliases and re-attaches ride free.
    private void ChargeShell()
    {
        if (_shellCharged || _memoryGovernor is null)
        {
            return;
        }

        _memoryGovernor.Reserve(SetShellBytes, _allocationSpan);
        _memoryGovernor.Commit(SetShellBytes);
        _shellCharged = true;
    }

    public PySet(PySet other)
    {
        _items = new HashSet<object>(other._items, PyValueComparer.Instance);
        _protocol = other._protocol is null ? null : new List<ProtocolEntry>(other._protocol);
        _protocolBytes = 0;
        _protocolSeq = other._protocolSeq;
        if (other._sideIndex is not null)
        {
            _sideIndex = new ProtocolSideIndex();
            _sideIndex.CloneFrom(other._sideIndex, null, null);
        }
        _memoryGovernor = other._memoryGovernor;
        _allocationSpan = other._allocationSpan;
        if (_memoryGovernor is not null)
        {
            EnsureCapacity(other.Count);
            AdoptInitialItems();
        }

        ChargeShell();
    }

    public PySet(PySet other, MemoryGovernor governor) : this(other, governor, null) { }

    public PySet(PySet other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(other._items, governor, allocationSpan)
    {
        // The enumerating ctor above homes every entry fast by identity;
        // re-index protocol items with their contextual hashes.
        if (other._protocol is not null)
        {
            _protocol = new List<ProtocolEntry>(other._protocol);
            _protocolSeq = other._protocolSeq;
            var bytes = checked(ProtocolEntryBytes * _protocol.Count);
            governor.Reserve(bytes, allocationSpan);
            governor.Commit(bytes);
            _protocolBytes = bytes;
        }

        if (other._sideIndex is not null)
        {
            _sideIndex = new ProtocolSideIndex();
            _sideIndex.CloneFrom(other._sideIndex, governor, allocationSpan);
        }
    }

    public int Count => _items.Count;

    public int Length => Count;

    // Current committed shell-plus-backing charges, for pooled owners that
    // release them if this set is dropped. Incremental growth after the
    // snapshot only ever leaves a safe residual behind; wholesale
    // replacement re-snapshots through Clear below. Adopted scalar coupons
    // fold in, so snapshots and drop sweeps carry them.
    internal long CommittedStorageBytes => (_shellCharged ? SetShellBytes + _committedBytes : _committedBytes) + _protocolBytes + (_sideIndex?.CommittedBytes ?? 0) + (_scalarCoupons?.CommittedBytes ?? 0);

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes) =>
        OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);

    public LythonSourceSpan? AllocationSpan => _allocationSpan;

    public bool Add(object item)
    {
        if (PyHashProtocols.NeedsProtocolKey(item))
        {
            return AddProtocolItem(item);
        }

        // At a growth boundary, probe first so a duplicate cannot allocate before
        // the memory governor approves the next table. Otherwise Add needs one lookup.
        // Committed capacity, not live CLR capacity, so usage past a failed growth
        // still routes through EnsureCapacity below.
        if (_protocol is not null && TryGetProtocolBuiltin(item, out _))
        {
            return false;
        }

        var committedBefore = CommittedStorageBytes;
        if (Count >= _capacity && _items.Contains(item))
        {
            return false;
        }

        EnsureCapacity(Count + 1);
        if (!_items.Add(item))
        {
            return false;
        }

        // Newly held: adopt, rolling the table insertion back when the coupon
        // denies so contents and coupons stay in sync.
        try
        {
            AdoptIncoming(item);
        }
        catch (Exception)
        {
            _ = _items.Remove(item);
            throw;
        }

        NoteGrowth(committedBefore);
        return true;
    }

    public void AttachMemoryGovernor(MemoryGovernor governor)
        => AttachMemoryGovernor(governor, null);

    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (_memoryGovernor is null)
        {
            _memoryGovernor = governor;
            _allocationSpan ??= allocationSpan;
            ChargeShell();
            return;
        }

        _allocationSpan ??= allocationSpan;
    }

    public bool Remove(object item)
    {
        if (_memoryGovernor is null || !AdoptedScalarCoupons.IsAdoptableScalar(item))
        {
            if (_items.Remove(item))
            {
                RemoveProtocolShadow(item);
                return true;
            }

            return RemoveProtocolItem(item);
        }

        // Governed and possibly adopted: resolve the stored identity first so the
        // coupon released is the held box, not a structural-twin argument.
        if (_items.TryGetValue(item, out var stored))
        {
            _ = _items.Remove(item);
            RemoveProtocolShadow(stored);
            ReleaseOutgoing(stored);
            return true;
        }

        return RemoveProtocolItem(item);
    }

    public bool TryPop(out object item)
    {
        using var enumerator = _items.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            item = PyNone.Instance;
            return false;
        }

        item = enumerator.Current;
        Remove(item);
        return true;
    }

    public bool Contains(object item)
    {
        if (_items.Contains(item))
        {
            return true;
        }

        if (PyHashProtocols.NeedsProtocolKey(item))
        {
            return ContainsProtocolItem(item);
        }

        return _protocol is not null && ContainsProtocolBuiltin(item);
    }

    public void Clear()
    {
        // The shell stays owned for the object lifetime; only backing capacity is released.
        if (_memoryGovernor is not null)
        {
            if (_committedBytes > 0)
            {
                _memoryGovernor.Release(_committedBytes);
                _committedBytes = 0;
                _capacity = 0;
            }

            if (_protocolBytes > 0)
            {
                _memoryGovernor.Release(_protocolBytes);
                _protocolBytes = 0;
            }

            _sideIndex?.ReleaseAll(_memoryGovernor);
            _sideIndex = null;
            ReleaseAllCoupons();
            _items = new HashSet<object>(PyValueComparer.Instance);
            _protocol = null;
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
            return;
        }

        _items.Clear();
        _protocol = null;
        _sideIndex = null;
        _protocolBytes = 0;
    }

    private LythonRuntime.ExecutionContext? ActiveProtocolContext()
        => PyStructuralGuard.GuestDispatchSuppressed ? null : PyStructuralGuard.AmbientContext;

    // Dual-home protocol insert: update nothing (sets hold no values),
    // returning false when the logical item already lives in either home.
    private bool AddProtocolItem(object item)
    {
        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        if (context is null || useSpan is null)
        {
            return AddProtocolStructural(item);
        }

        var hash = PyHashProtocols.GetProtocolHash(item, context, useSpan);
        var structuralHash = PyValueComparer.Instance.GetHashCode(item);
        // N10 index: duplicate checks scan only hash-bucketed candidates (same dispatch
        // set and order); the store phase below is untouched.
        var work = 0;
        if (_protocol is not null)
        {
            var candidates = SnapshotProtocolCandidates(hash, structuralHash, useSpan);
            for (var i = 0; i < candidates.Length; i++)
            {
                if ((++work & 63) == 0)
                {
                    context.CheckExecutionBudget(useSpan);
                }

                var entry = candidates[i];
                if (ReferenceEquals(entry.Item, item) || LythonRuntime.ElementEquals(entry.Item, item, context, useSpan))
                {
                    return false;
                }
            }
        }

        EnsureStoreSnapshot(_items.Count, useSpan);
        var storeHit = -1;
        var probe = 0;
        foreach (var existing in _items)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecutionBudget(useSpan);
            }

            if (PyValueComparer.Instance.GetHashCode(existing) == hash || PyValueComparer.Instance.GetHashCode(existing) == structuralHash)
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

                var existing = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(existing) == hash || PyValueComparer.Instance.GetHashCode(existing) == structuralHash) && (ReferenceEquals(existing, item) || LythonRuntime.ElementEquals(existing, item, context, useSpan)))
                {
                    return false;
                }
            }
        }

        AppendProtocolItem(item, hash, structuralHash);
        return true;
    }

    private bool AddProtocolStructural(object item)
    {
        if (_protocol is not null)
        {
            foreach (var entry in _protocol)
            {
                if (ReferenceEquals(entry.Item, item))
                {
                    return false;
                }
            }
        }

        var structuralHash = PyValueComparer.Instance.GetHashCode(item);
        AppendProtocolItem(item, structuralHash, structuralHash);
        return true;
    }

    private void AppendProtocolItem(object item, int hash, int structuralHash)
    {
        var committedBefore = CommittedStorageBytes;
        // The side entry retains the argument even when the fast table already
        // holds a structural twin, so the coupon commits before any denial point.
        // A denied protocol charge needs no extra rollback: the stranded entry
        // stays retained (side list and index alike), exactly what the coupons cover.
        AdoptIncoming(item);
        _protocol ??= new List<ProtocolEntry>();
        _sideIndex ??= new ProtocolSideIndex();
        var position = _protocol.Count;
        try
        {
            _sideIndex.Add(position, hash, structuralHash, _memoryGovernor, _allocationSpan);
        }
        catch (Exception)
        {
            UnadoptIncoming(item);
            throw;
        }

        try
        {
            EnsureCapacity(Count + 1);
        }
        catch (Exception)
        {
            _sideIndex.RemoveForRollback(position, hash, structuralHash, _memoryGovernor);
            UnadoptIncoming(item);
            throw;
        }

        _ = _items.Add(item);
        _protocol.Add(new ProtocolEntry(_protocolSeq++, hash, structuralHash, item));
        if (_memoryGovernor is not null)
        {
            _memoryGovernor.Reserve(ProtocolEntryBytes, _allocationSpan);
            _memoryGovernor.Commit(ProtocolEntryBytes);
            _protocolBytes += ProtocolEntryBytes;
        }

        NoteGrowth(committedBefore);
    }

    private bool ContainsProtocolItem(object item)
    {
        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        if (_protocol is null)
        {
            return false;
        }

        if (context is null || useSpan is null)
        {
            foreach (var entry in _protocol)
            {
                if (ReferenceEquals(entry.Item, item))
                {
                    return true;
                }
            }

            return false;
        }

        var hash = PyHashProtocols.GetProtocolHash(item, context, useSpan);
        var structuralHash = PyValueComparer.Instance.GetHashCode(item);
        // N10 index: membership scans only hash-bucketed candidates in insertion order
        // (same dispatch set and order as the linear scan). There is no store phase here.
        // Candidate copies preflight only candidates; work checks run where dispatch happens.
        var work = 0;
        var candidates = SnapshotProtocolCandidates(hash, structuralHash, useSpan);
        for (var i = 0; i < candidates.Length; i++)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecutionBudget(useSpan);
            }

            var entry = candidates[i];
            if (ReferenceEquals(entry.Item, item) || LythonRuntime.ElementEquals(entry.Item, item, context, useSpan))
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsProtocolBuiltin(object item)
    {
        if (_protocol is null)
        {
            return false;
        }

        return TryGetProtocolBuiltin(item, out _);
    }

    private bool TryGetProtocolBuiltin(object item, out object _)
    {
        _ = PyNone.Instance;
        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        var hash = PyValueComparer.Instance.GetHashCode(item);
        // N10 index: builtin probes scan only hash-bucketed candidates in insertion order
        // (same dispatch set: entry protocol hash only). Snapshotting the candidates keeps
        // mutating guest == safe like every other scan.
        var work = 0;
        var candidates = SnapshotBuiltinCandidates(hash, useSpan);
        for (var i = 0; i < candidates.Length; i++)
        {
            // N10 part 1: budget non-collision scans too; the context may be
            // null on context-free paths (identity fast path only then).
            if ((++work & 63) == 0 && context is not null && useSpan is not null)
            {
                context.CheckExecutionBudget(useSpan);
            }

            var entry = candidates[i];
            if (ReferenceEquals(entry.Item, item))
            {
                return true;
            }

            if (context is not null && useSpan is not null && LythonRuntime.ElementEquals(entry.Item, item, context, useSpan))
            {
                return true;
            }
        }

        return false;
    }

    private bool RemoveProtocolItem(object item)
    {
        var context = ActiveProtocolContext();
        var useSpan = PyStructuralGuard.AmbientSpan ?? _allocationSpan;
        if (context is null || useSpan is null)
        {
            return RemoveProtocolStructural(item);
        }

        // N10 part 1: with no protocol entries homed, a builtin item that
        // missed the structural probe cannot match anything the scan would
        // reach without guest dispatch, so skip both snapshots entirely.
        // (A protocol item still scans the store: an always-true __eq__ can
        // match a builtin-homed entry like CPython.)
        if ((_protocol is null || _protocol.Count == 0) && !PyHashProtocols.NeedsProtocolKey(item))
        {
            return false;
        }

        var hash = PyHashProtocols.GetProtocolHash(item, context, useSpan);
        var structuralHash = PyValueComparer.Instance.GetHashCode(item);
        // N10 index: removal scans only hash-bucketed candidates (same dispatch set and
        // order); the store phase below is untouched. Position fixup rides RemoveSideEntry.
        var work = 0;
        if (_protocol is not null)
        {
            var candidates = SnapshotProtocolCandidates(hash, structuralHash, useSpan);
            for (var i = 0; i < candidates.Length; i++)
            {
                if ((++work & 63) == 0)
                {
                    context.CheckExecutionBudget(useSpan);
                }

                var entry = candidates[i];
                if (ReferenceEquals(entry.Item, item) || LythonRuntime.ElementEquals(entry.Item, item, context, useSpan))
                {
                    RemoveSideEntry(entry.Item);
                    return true;
                }
            }
        }

        EnsureStoreSnapshot(_items.Count, useSpan);
        var storeHit = -1;
        var probe = 0;
        foreach (var existing in _items)
        {
            if ((++work & 63) == 0)
            {
                context.CheckExecutionBudget(useSpan);
            }

            if (PyValueComparer.Instance.GetHashCode(existing) == hash || PyValueComparer.Instance.GetHashCode(existing) == structuralHash)
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

                var existing = storeSnapshot[i];
                if ((PyValueComparer.Instance.GetHashCode(existing) == hash || PyValueComparer.Instance.GetHashCode(existing) == structuralHash) && (ReferenceEquals(existing, item) || LythonRuntime.ElementEquals(existing, item, context, useSpan)))
                {
                    _ = _items.Remove(existing);
                    RemoveProtocolShadow(existing);
                    ReleaseOutgoing(existing);
                    return true;
                }
            }
        }

        return false;
    }

    private bool RemoveProtocolStructural(object item)
    {
        if (_protocol is not null)
        {
            for (var i = 0; i < _protocol.Count; i++)
            {
                if (ReferenceEquals(_protocol[i].Item, item))
                {
                    RemoveSideEntry(_protocol[i].Item);
                    return true;
                }
            }
        }

        return false;
    }

    private void RemoveProtocolShadow(object item)
    {
        if (_protocol is null)
        {
            return;
        }

        for (var i = 0; i < _protocol.Count; i++)
        {
            if (ReferenceEquals(_protocol[i].Item, item))
            {
                var shadow = _protocol[i];
                _protocol.RemoveAt(i);
                _sideIndex?.RemoveAt(i, shadow.Hash, shadow.StructuralHash);
                ReleaseSideBytes(ProtocolEntryBytes);
                return;
            }
        }
    }

    private void RemoveSideEntry(object item)
    {
        // Callers always pass the stored entry item (side-scan and structural
        // matches resolve it first), so the released coupon is the evicted box.
        if (_protocol is not null)
        {
            for (var i = 0; i < _protocol.Count; i++)
            {
                if (ReferenceEquals(_protocol[i].Item, item))
                {
                    var evicted = _protocol[i];
                    ReleaseOutgoing(evicted.Item);
                    _protocol.RemoveAt(i);
                    _sideIndex?.RemoveAt(i, evicted.Hash, evicted.StructuralHash);
                    break;
                }
            }
        }

        _ = _items.Remove(item);
        ReleaseSideBytes(ProtocolEntryBytes);
    }

    // N10 index: snapshot only the side-list candidates for a lookup, in insertion order.
    // CollectCandidates merges both hash buckets (either key can match, like the dual-key
    // linear scans this replaces); the filter below restores the exact dispatch set so no
    // extra guest == runs. The copy preflights only candidates, never the whole list.
    // The transient position set is scratch bounded by the charged population; the retained
    // copy is governed. Sorted positions reproduce the old linear dispatch order (every
    // match at or after the first hit, ascending). Store scans stay untouched.
    private ProtocolEntry[] SnapshotProtocolCandidates(int hash, int structuralHash, LythonSourceSpan? span)
    {
        if (_protocol is null)
        {
            return [];
        }

        if (_sideIndex is null)
        {
            _memoryGovernor?.EnsureCanReserve(checked(ProtocolEntryBytes * (long)_protocol.Count), span ?? _allocationSpan);
            var fallback = new List<ProtocolEntry>(_protocol.Count);
            foreach (var entry in _protocol)
            {
                if (entry.Hash == hash || entry.StructuralHash == structuralHash)
                {
                    fallback.Add(entry);
                }
            }

            return fallback.ToArray();
        }

        var positions = new HashSet<int>();
        _sideIndex.CollectCandidates(hash, structuralHash, positions);
        if (positions.Count == 0)
        {
            return [];
        }

        _memoryGovernor?.EnsureCanReserve(checked(ProtocolEntryBytes * (long)positions.Count), span ?? _allocationSpan);
        var ordered = new List<int>(positions);
        ordered.Sort();
        var snapshot = new List<ProtocolEntry>(ordered.Count);
        foreach (var position in ordered)
        {
            var entry = _protocol[position];
            if (entry.Hash == hash || entry.StructuralHash == structuralHash)
            {
                snapshot.Add(entry);
            }
        }

        return snapshot.ToArray();
    }

    // Builtin-item variant: dispatch runs only where the entry protocol hash matches,
    // like the linear scan. Buckets merge both keys, so the same filter applies.
    private ProtocolEntry[] SnapshotBuiltinCandidates(int hash, LythonSourceSpan? span)
    {
        if (_protocol is null)
        {
            return [];
        }

        if (_sideIndex is null)
        {
            _memoryGovernor?.EnsureCanReserve(checked(ProtocolEntryBytes * (long)_protocol.Count), span ?? _allocationSpan);
            var fallback = new List<ProtocolEntry>(_protocol.Count);
            foreach (var entry in _protocol)
            {
                if (entry.Hash == hash)
                {
                    fallback.Add(entry);
                }
            }

            return fallback.ToArray();
        }

        var positions = new HashSet<int>();
        _sideIndex.CollectCandidates(hash, hash, positions);
        if (positions.Count == 0)
        {
            return [];
        }

        _memoryGovernor?.EnsureCanReserve(checked(ProtocolEntryBytes * (long)positions.Count), span ?? _allocationSpan);
        var ordered = new List<int>(positions);
        ordered.Sort();
        var snapshot = new List<ProtocolEntry>(ordered.Count);
        foreach (var position in ordered)
        {
            var entry = _protocol[position];
            if (entry.Hash == hash)
            {
                snapshot.Add(entry);
            }
        }

        return snapshot.ToArray();
    }

    // N10 part 1: side-table and store snapshots deny before they can
    // allocate. Sizes follow the tracked entry rate and the snapshot
    // estimator; unowned sets skip silently like all other paths.
    private void EnsureSideSnapshot(int count, LythonSourceSpan? span)
        => _memoryGovernor?.EnsureCanReserve(checked(ProtocolEntryBytes * (long)count), span ?? _allocationSpan);

    private void EnsureStoreSnapshot(int count, LythonSourceSpan? span)
        => _memoryGovernor?.EnsureCanReserve(EstimateSnapshotBytes(count), span ?? _allocationSpan);

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

    public void UnionWith(PySet other)
    {
        foreach (var item in other._items)
        {
            _ = Add(item);
        }
    }

    public void IntersectWith(PySet other)
    {
        var governor = _memoryGovernor;
        if (governor is null)
        {
            _items.IntersectWith(other._items);
            return;
        }

        // The filter list holds only references to already-owned items, but
        // its backing array coexists with the live table, so cover it with a
        // transient reservation sized by the exact upper bound.
        using var scratch = governor.ReserveTemporary(EstimateFilterBytes(Count), _allocationSpan);
        RebuildFrom(FilterContained(other, keepContained: true));
    }

    public void ExceptWith(PySet other)
    {
        var governor = _memoryGovernor;
        if (governor is null)
        {
            _items.ExceptWith(other._items);
            return;
        }

        // Same transient filter scratch as IntersectWith above.
        using var scratch = governor.ReserveTemporary(EstimateFilterBytes(Count), _allocationSpan);
        RebuildFrom(FilterContained(other, keepContained: false));
    }

    public void SymmetricExceptWith(PySet other)
    {
        var governor = _memoryGovernor;
        if (governor is null)
        {
            _items.SymmetricExceptWith(other._items);
            return;
        }

        // Both tables stay live while the symmetric list is built, so the
        // transient reservation covers the exact combined upper bound.
        using var scratch = governor.ReserveTemporary(EstimateFilterBytes(Count + other.Count), _allocationSpan);
        var symmetric = new List<object>(Count + other.Count);
        foreach (var item in _items)
        {
            if (!other.Contains(item))
            {
                symmetric.Add(item);
            }
        }

        foreach (var item in other._items)
        {
            if (!Contains(item))
            {
                symmetric.Add(item);
            }
        }

        RebuildFrom(symmetric);
    }

    // Protocol-involved comparisons go manual (counts plus both-direction
    // Contains, which observes ambient provenance); pure-builtin pairs keep
    // the HashSet fast path bit-for-bit.
    private bool HasProtocolItems(PySet other)
        => (_protocol is not null && _protocol.Count > 0) || (other._protocol is not null && other._protocol.Count > 0);

    public bool SetEquals(PySet other)
    {
        if (HasProtocolItems(other))
        {
            return Count == other.Count && IsSubsetOf(other) && other.IsSubsetOf(this);
        }

        return _items.SetEquals(other._items);
    }

    public bool IsSubsetOf(PySet other)
    {
        if (HasProtocolItems(other))
        {
            foreach (var item in _items)
            {
                if (!other.Contains(item))
                {
                    return false;
                }
            }

            return true;
        }

        return _items.IsSubsetOf(other._items);
    }

    public bool IsProperSubsetOf(PySet other)
    {
        if (HasProtocolItems(other))
        {
            return Count < other.Count && IsSubsetOf(other);
        }

        return _items.IsProperSubsetOf(other._items);
    }

    public bool IsSupersetOf(PySet other)
    {
        if (HasProtocolItems(other))
        {
            return other.IsSubsetOf(this);
        }

        return _items.IsSupersetOf(other._items);
    }

    public bool IsProperSupersetOf(PySet other)
    {
        if (HasProtocolItems(other))
        {
            return Count > other.Count && IsSupersetOf(other);
        }

        return _items.IsProperSupersetOf(other._items);
    }

    public bool IsTruthy() => Count != 0;

    public IEnumerable<object> Iterate() => this;

    public PyString RenderPython(PyRenderingContext context) => PyRendering.ToReprPyString(this, context);

    public PyString RenderInterpolated(PyRenderingContext context) => PyRendering.ToReprPyString(this, context);

    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    private void EnsureCapacity(int targetCount)
    {
        if (targetCount <= 0)
        {
            return;
        }

        if (targetCount <= _capacity)
        {
            return;
        }

        if (_memoryGovernor is not null)
        {
            _memoryGovernor.EnsureCanReserve(EstimateSlots(_capacity, targetCount), _allocationSpan);
        }

        var newCapacity = _items.EnsureCapacity(targetCount);
        if (_memoryGovernor is not null && newCapacity > _capacity)
        {
            var bytes = EstimateSlots(_capacity, newCapacity);
            _memoryGovernor.Reserve(bytes, _allocationSpan);
            _memoryGovernor.Commit(bytes);
            _committedBytes += bytes;
        }

        _capacity = newCapacity;
    }

    private static long EstimateSlots(int previousCapacity, int newCapacity)
        => (24L * (newCapacity - previousCapacity)) + (previousCapacity == 0 ? 80L : 0L);

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
    // a coupon denies: callers publish nothing on failure. AdoptAll rolls its
    // own coupons back, so only shell, backing and protocol refund here.
    private void AdoptInitialItems()
    {
        var incoming = new AdoptedScalarCoupons();
        try
        {
            incoming.AdoptAll(_items, _memoryGovernor!, _allocationSpan);
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

        _committedBytes = 0;
        _protocolBytes = 0;
        _sideIndex = null;
        _capacity = 0;
        _shellCharged = false;
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

    private List<object> FilterContained(PySet other, bool keepContained)
    {
        var result = new List<object>(Count);
        foreach (var item in _items)
        {
            if (other.Contains(item) == keepContained)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private void RebuildFrom(List<object> items)
    {
        var committedBefore = CommittedStorageBytes;
        _items.Clear();
        ReleaseAllCoupons();
        if (_protocol is not null)
        {
            if (_memoryGovernor is not null && _protocolBytes > 0)
            {
                _memoryGovernor.Release(_protocolBytes);
            }

            _protocol = null;
            _protocolBytes = 0;
        }

        if (_sideIndex is not null)
        {
            _sideIndex.ReleaseAll(_memoryGovernor);
            _sideIndex = null;
        }

        EnsureCapacity(items.Count);
        foreach (var item in items)
        {
            Add(item);
        }

        NoteGrowth(committedBefore);
    }
}
