using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PySet : IEnumerable<object>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue
{
    private HashSet<object> _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private long _committedBytes;

    // Protocol-item side index (R13b): items needing __hash__/__eq__ dispatch
    // bypass HashSet probing (which cannot run guest code) and scan with
    // precomputed hashes plus element ==. Dual-homed like PyDict: every
    // entry lives in the fast table (iteration, Count, copies untouched)
    // with a shadow here carrying its contextual hash for dispatch scans.
    private List<ProtocolEntry>? _protocol;
    private long _protocolBytes;
    private long _protocolSeq;

    internal readonly record struct ProtocolEntry(long Seq, int Hash, object Item);

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

            return;
        }

        // Lazy source: no count exists to pre-size from, so Add governs each
        // growth step with the committed-capacity atomicity above instead of
        // draining the whole source into an uncharged array first.
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
        _memoryGovernor = other._memoryGovernor;
        _allocationSpan = other._allocationSpan;
        if (_memoryGovernor is not null)
        {
            EnsureCapacity(other.Count);
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
    }

    public int Count => _items.Count;

    public int Length => Count;

    // Current committed shell-plus-backing charges, for pooled owners that
    // release them if this set is dropped. Incremental growth after the
    // snapshot only ever leaves a safe residual behind; wholesale
    // replacement re-snapshots through Clear below.
    internal long CommittedStorageBytes => (_shellCharged ? SetShellBytes + _committedBytes : _committedBytes) + _protocolBytes;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

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

        if (Count < _capacity)
        {
            return _items.Add(item);
        }

        if (_items.Contains(item))
        {
            return false;
        }

        var committedBefore = _committedBytes;
        EnsureCapacity(Count + 1);
        var added = _items.Add(item);
        if (added && _committedBytes != committedBefore)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }

        return added;
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
        if (_items.Remove(item))
        {
            RemoveProtocolShadow(item);
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

            _items = new HashSet<object>(PyValueComparer.Instance);
            _protocol = null;
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
            return;
        }

        _items.Clear();
        _protocol = null;
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
        if (_protocol is not null)
        {
            var snapshot = _protocol.ToArray();
            foreach (var entry in snapshot)
            {
                if (entry.Hash == hash && (ReferenceEquals(entry.Item, item) || LythonRuntime.ElementEquals(entry.Item, item, context, useSpan)))
                {
                    return false;
                }
            }
        }

        foreach (var existing in _items.ToArray())
        {
            if (PyValueComparer.Instance.GetHashCode(existing) == hash && (ReferenceEquals(existing, item) || LythonRuntime.ElementEquals(existing, item, context, useSpan)))
            {
                return false;
            }
        }

        AppendProtocolItem(item, hash);
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

        AppendProtocolItem(item, PyValueComparer.Instance.GetHashCode(item));
        return true;
    }

    private void AppendProtocolItem(object item, int hash)
    {
        var committedBefore = CommittedStorageBytes;
        EnsureCapacity(Count + 1);
        _ = _items.Add(item);
        _protocol ??= new List<ProtocolEntry>();
        _protocol.Add(new ProtocolEntry(_protocolSeq++, hash, item));
        if (_memoryGovernor is not null)
        {
            _memoryGovernor.Reserve(ProtocolEntryBytes, _allocationSpan);
            _memoryGovernor.Commit(ProtocolEntryBytes);
            _protocolBytes += ProtocolEntryBytes;
        }

        if (CommittedStorageBytes != committedBefore)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }
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
        var snapshot = _protocol.ToArray();
        foreach (var entry in snapshot)
        {
            if (entry.Hash == hash && (ReferenceEquals(entry.Item, item) || LythonRuntime.ElementEquals(entry.Item, item, context, useSpan)))
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
        foreach (var entry in _protocol!)
        {
            if (entry.Hash != hash)
            {
                continue;
            }

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

        var hash = PyHashProtocols.GetProtocolHash(item, context, useSpan);
        if (_protocol is not null)
        {
            var snapshot = _protocol.ToArray();
            foreach (var entry in snapshot)
            {
                if (entry.Hash == hash && (ReferenceEquals(entry.Item, item) || LythonRuntime.ElementEquals(entry.Item, item, context, useSpan)))
                {
                    RemoveSideEntry(entry.Item);
                    return true;
                }
            }
        }

        foreach (var existing in _items.ToArray())
        {
            if (PyValueComparer.Instance.GetHashCode(existing) == hash && (ReferenceEquals(existing, item) || LythonRuntime.ElementEquals(existing, item, context, useSpan)))
            {
                _ = _items.Remove(existing);
                RemoveProtocolShadow(existing);
                return true;
            }
        }

        return false;
    }

    private bool RemoveProtocolStructural(object item)
    {
        if (_protocol is not null)
        {
            foreach (var entry in _protocol.ToArray())
            {
                if (ReferenceEquals(entry.Item, item))
                {
                    RemoveSideEntry(entry.Item);
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
                _protocol.RemoveAt(i);
                ReleaseSideBytes(ProtocolEntryBytes);
                return;
            }
        }
    }

    private void RemoveSideEntry(object item)
    {
        if (_protocol is not null)
        {
            for (var i = 0; i < _protocol.Count; i++)
            {
                if (ReferenceEquals(_protocol[i].Item, item))
                {
                    _protocol.RemoveAt(i);
                    break;
                }
            }
        }

        _ = _items.Remove(item);
        ReleaseSideBytes(ProtocolEntryBytes);
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
        _items.Clear();
        var hadProtocol = _protocol is not null;
        if (hadProtocol)
        {
            if (_memoryGovernor is not null && _protocolBytes > 0)
            {
                _memoryGovernor.Release(_protocolBytes);
            }

            _protocol = null;
            _protocolBytes = 0;
        }

        EnsureCapacity(items.Count);
        foreach (var item in items)
        {
            Add(item);
        }

        if (hadProtocol)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }
    }
}
