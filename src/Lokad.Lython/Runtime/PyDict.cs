using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue, IChainMapSource
{
    private IPyDictStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private int _version;

    // Protocol-key side table (R13b): keys needing __hash__/__eq__ dispatch
    // bypass Dictionary probing (which cannot run guest code) and scan with
    // precomputed hashes plus element ==. The first protocol insert migrates
    // fast entries over with comparer hashes (identical by construction), so
    // iteration order stays exact and later ops stay unified; the fast store
    // stays empty afterwards and every later insert lands in the side table.
    private List<ProtocolEntry>? _protocol;
    private long _protocolBytes;
    private long _protocolSeq;

    internal readonly record struct ProtocolEntry(long Seq, int Hash, object Key);

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
    }

    // The side index holds shadows of fast-store entries, never additional
    // entries, so only the fast store counts. Its backing bytes still add
    // to CommittedStorageBytes below.
    public int Count => _items.Count;

    object IChainMapSource.Underlying => this;

    // Current committed backing charges, for pooled owners that release them
    // if this dictionary is dropped without wholesale storage replacement.
    internal long CommittedStorageBytes => _items.CommittedBytes + _protocolBytes;

    public int Length => Count;

    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;

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
        if (_items.TrySetExisting(storageKey, storageValue))
        {
            return;
        }

        var committedBefore = _items.CommittedBytes;
        _items = PyDictStorage.EnsureCapacity(_items, Count + 1, _memoryGovernor, _allocationSpan);
        _items.AddNew(storageKey, storageValue);
        _version++;
        if (_items.CommittedBytes != committedBefore)
        {
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
        }
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
        if (_items.Remove(ToStorageKey(key)))
        {
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
        if (_protocol is not null)
        {
            var snapshot = _protocol.ToArray();
            foreach (var entry in snapshot)
            {
                if (entry.Hash == hash && (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan)))
                {
                    if (_items.TryGetValue(ToStorageKey(entry.Key), out var stored))
                    {
                        value = FromStorageValue(stored);
                        return true;
                    }
                }
            }
        }

        foreach (var pair in _items)
        {
            if (PyValueComparer.Instance.GetHashCode(pair.Key) == hash && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(FromStorageKey(pair.Key), key, context, useSpan)))
            {
                value = FromStorageValue(pair.Value);
                return true;
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
        foreach (var entry in _protocol)
        {
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

        value = PyNone.Instance;
        return false;
    }

    // Ensures the side index exists; entries are charged per slot below.
    private void EnsureSideTable()
    {
        _protocol ??= new List<ProtocolEntry>();
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
        if (_protocol is not null)
        {
            var snapshot = _protocol.ToArray();
            foreach (var entry in snapshot)
            {
                if (entry.Hash == hash && (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan)))
                {
                    _ = _items.SetItem(ToStorageKey(entry.Key), storageValue);
                    return;
                }
            }
        }

        foreach (var pair in _items.ToArray())
        {
            if (PyValueComparer.Instance.GetHashCode(pair.Key) == hash && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(FromStorageKey(pair.Key), key, context, useSpan)))
            {
                _ = _items.SetItem(ToStorageKey(pair.Key), storageValue);
                return;
            }
        }

        AppendProtocolItem(key, storageValue, hash);
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
                    _ = _items.SetItem(ToStorageKey(entry.Key), storageValue);
                    return;
                }
            }
        }

        AppendProtocolItem(key, storageValue, PyValueComparer.Instance.GetHashCode(key));
    }

    private void AppendProtocolItem(object key, object storageValue, int hash)
    {
        var committedBefore = CommittedStorageBytes;
        _items = PyDictStorage.EnsureCapacity(_items, checked(_items.Count + 1), _memoryGovernor, _allocationSpan);
        _items.AddNew(ToStorageKey(key), storageValue);
        EnsureSideTable();
        _protocol!.Add(new ProtocolEntry(_protocolSeq++, hash, ToStorageKey(key)));
        _version++;
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

        var hash = PyHashProtocols.GetProtocolHash(key, context, useSpan);
        if (_protocol is not null)
        {
            var snapshot = _protocol.ToArray();
            foreach (var entry in snapshot)
            {
                if (entry.Hash == hash && (ReferenceEquals(entry.Key, key) || LythonRuntime.ElementEquals(entry.Key, key, context, useSpan)))
                {
                    RemoveSideEntry(entry.Key);
                    return true;
                }
            }
        }

        foreach (var pair in _items.ToArray())
        {
            if (PyValueComparer.Instance.GetHashCode(pair.Key) == hash && (ReferenceEquals(pair.Key, key) || LythonRuntime.ElementEquals(FromStorageKey(pair.Key), key, context, useSpan)))
            {
                _items.Remove(ToStorageKey(pair.Key));
                RemoveProtocolShadow(pair.Key);
                return true;
            }
        }

        return false;
    }

    private bool RemoveProtocolStructural(object key)
    {
        if (_protocol is not null)
        {
            foreach (var entry in _protocol.ToArray())
            {
                if (ReferenceEquals(entry.Key, key))
                {
                    RemoveSideEntry(entry.Key);
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
        if (_protocol is not null)
        {
            for (var i = 0; i < _protocol.Count; i++)
            {
                if (ReferenceEquals(_protocol[i].Key, key))
                {
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

    private void EnsureUnmodified(int expectedVersion)
    {
        if (_version != expectedVersion)
        {
            throw new LythonRuntimeException("RuntimeError", "dictionary changed size during iteration", _allocationSpan);
        }
    }
}
