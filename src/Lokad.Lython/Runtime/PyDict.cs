using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class PyDict : IEnumerable<KeyValuePair<object, object>>, IPyDictStorage, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue, IChainMapSource, IPyOwnershipSnapshot
{
    private IPyDictStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private int _version;
    private readonly bool _keysOnly;
    internal static readonly LythonSourceSpan UnknownKeySpan = new(0, 0, 0, 0);
    internal void NotifyNamespaceMutation() => _version++;

    public PyDict() : this(0, null, null) { }
    public PyDict(MemoryGovernor governor) : this(governor, null) { }
    public PyDict(MemoryGovernor governor, LythonSourceSpan? allocationSpan) : this(0, governor, allocationSpan) { }
    internal PyDict(IPyDictStorage storage, MemoryGovernor governor, LythonSourceSpan allocationSpan)
    { _items = storage; _memoryGovernor = governor; _allocationSpan = allocationSpan; }
    internal PyDict(int capacity, MemoryGovernor? governor, LythonSourceSpan? allocationSpan, bool keysOnly = false)
    {
        _memoryGovernor = governor; _allocationSpan = allocationSpan;
        _keysOnly = keysOnly;
        InitializeNativeStorage(capacity, governor, allocationSpan);
        _items = this;
    }
    public PyDict(PyDict other) : this(other, other._memoryGovernor, other._allocationSpan) { }
    public PyDict(PyDict other, MemoryGovernor governor) : this(other, governor, null) { }
    public PyDict(PyDict other, MemoryGovernor? governor, LythonSourceSpan? allocationSpan)
        : this(other._nativeItems?.Length ?? other.Count, governor, allocationSpan, other._keysOnly)
    {
        try
        {
            if (other._nativeItems is not null)
            {
                // Copy the one table, including stored hashes and insertion
                // identities. No key is rehashed or repackaged during a copy.
                for (var offset = 0; offset < other._nativeItems.Length; offset += 4096)
                {
                    PyStructuralGuard.NoteWork(null, allocationSpan);
                    Array.Copy(other._nativeItems, offset, _nativeItems!, offset,
                        Math.Min(4096, other._nativeItems.Length - offset));
                }
                _nativeCount = other._nativeCount;
                _nativeProtocolCount = other._nativeProtocolCount;
                _nativeSequence = other._nativeSequence;
            }
            else CopyNativeEntries(other._items.Entries, governor, allocationSpan);
        }
        catch { RefundAbortedConstruction(); throw; }
    }

    public int Count => _items.Count;
    object IChainMapSource.Underlying => this;
    internal long CommittedStorageBytes => _items.CommittedBytes;
    public int Length => Count;
    internal bool HasProtocolKeys => _nativeItems is null ? _items.HasProtocolKeys : _nativeProtocolCount != 0;
    public MemoryGovernor? OwnerMemoryGovernor => _memoryGovernor;
    bool IPyOwnershipSnapshot.TrySnapshotOwnership(out long chargeBytes)
        => OwnershipSnapshot.Owned(OwnerMemoryGovernor, CommittedStorageBytes, out chargeBytes);
    public LythonSourceSpan? AllocationSpan => _allocationSpan;
    public IEnumerable<object> Keys => EnumerateKeys(_version);
    private IEnumerable<object> EnumerateKeys(int expectedVersion)
    {
        using var enumerator = _items.Keys.GetEnumerator();
        while (true)
        {
            EnsureUnmodified(expectedVersion);
            if (!enumerator.MoveNext()) yield break;
            yield return enumerator.Current;
        }
    }
    public IEnumerable<object> Values => EnumerateValues(_version);
    private IEnumerable<object> EnumerateValues(int expectedVersion)
    {
        using var enumerator = _items.Values.GetEnumerator();
        while (true)
        {
            EnsureUnmodified(expectedVersion);
            if (!enumerator.MoveNext()) yield break;
            yield return enumerator.Current;
        }
    }
    public IEnumerable<KeyValuePair<object, object>> Items => this;
    public object GetItem(object key) => _items.GetRequired(key);

    private LythonRuntime.ExecutionContext? ActiveProtocolContext()
        => PyStructuralGuard.GuestDispatchSuppressed ? null : PyStructuralGuard.AmbientContext;
    private LythonSourceSpan QuerySpan => PyStructuralGuard.AmbientSpan ?? _allocationSpan ?? UnknownKeySpan;

    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value)
    {
        if (_nativeItems is null) return TryGetBridgeValue(key, ActiveProtocolContext(), QuerySpan, out value);
        var context = ActiveProtocolContext();
        var span = QuerySpan;
        var protocol = PyHashProtocols.NeedsProtocolKey(key);
        var hash = QueryHash(key, context, span, protocol);
        var index = ResolveNativeKey(key, hash, protocol, context, span);
        value = index < 0 ? PyNone.Instance : _nativeItems[index].Value!;
        return index >= 0;
    }
    public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_nativeItems is null) return TryGetBridgeValue(key, context, span, out value);
        var protocol = PyHashProtocols.NeedsProtocolKey(key);
        var hash = QueryHash(key, context, span, protocol);
        var index = ResolveNativeKey(key, hash, protocol, context, span);
        value = index < 0 ? PyNone.Instance : _nativeItems[index].Value!;
        return index >= 0;
    }
    internal ValueTask<(bool Found, object Value)> TryGetBuiltinKeyAsync(
        object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (_nativeItems is null) return LookupBridgeAsync();
        if (!HasProtocolKeys)
            return new(TryGetValue(key, out var value, context, span) ? (true, value) : (false, PyNone.Instance));
        return LookupAsync();
        async ValueTask<(bool Found, object Value)> LookupBridgeAsync()
        {
            var protocol = PyHashProtocols.NeedsProtocolKey(key);
            var hash = QueryHash(key, context, span, protocol);
            var stored = await ResolveBridgeKeyAsync(key, hash, protocol, context, span, asynchronous: true).ConfigureAwait(false);
            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            return stored is not null && _items.TryGetValue(stored, out var found) ? (true, found) : (false, PyNone.Instance);
        }
        async ValueTask<(bool Found, object Value)> LookupAsync()
        {
            var protocol = PyHashProtocols.NeedsProtocolKey(key);
            var hash = QueryHash(key, context, span, protocol);
            var index = await ResolveNativeKeyAsync(key, hash, protocol, context, span).ConfigureAwait(false);
            return index < 0 ? (false, PyNone.Instance) : (true, _nativeItems[index].Value!);
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
        _memoryGovernor?.ThrowIfAdmissionFailed();
        var before = Count;
        try
        {
            if (_nativeItems is null)
            {
                var bridgeContext = ActiveProtocolContext();
                if (bridgeContext is null) { if (!_items.TrySetExisting(key, value)) _items.AddNew(key, value); return; }
                var bridgeProtocol = PyHashProtocols.NeedsProtocolKey(key);
                var hash = QueryHash(key, bridgeContext, QuerySpan, bridgeProtocol);
                var stored = ResolveBridgeKeyAsync(key, hash, bridgeProtocol, bridgeContext, QuerySpan, asynchronous: false).GetAwaiter().GetResult();
                if (stored is not null) _items.TrySetExisting(stored, value);
                else _items.AddNew(key, value, hash);
                return;
            }
            var context = ActiveProtocolContext();
            var span = QuerySpan;
            var protocol = PyHashProtocols.NeedsProtocolKey(key);
            SetNativeValue(key, value, QueryHash(key, context, span, protocol), protocol, context, span, replaceExisting: true);
        }
        finally { if (Count > before) _version++; }
    }
    internal void AddKeysFrom(PyDict source, LythonRuntime.ExecutionContext? context, LythonSourceSpan span)
    {
        var expectedVersion = source._version;
        using var retained = _memoryGovernor?.ReserveTemporary(source.CommittedStorageBytes, span);
        if (_nativeItems is not null && source._nativeItems is not null)
        {
            for (var i = 0; i < source._nativeCount; i++)
            {
                context?.CheckExecution(span);
                source.EnsureUnmodified(expectedVersion);
                var entry = source._nativeItems[i];
                var before = Count;
                try { SetNativeValue(entry.Key!, PyNone.Instance, entry.Hash, entry.Protocol, context, span, replaceExisting: false); }
                finally { if (Count > before) _version++; }
                source.EnsureUnmodified(expectedVersion);
            }
            return;
        }
        foreach (var pair in source)
        {
            context?.CheckExecution(span);
            if (context is null ? !TryGetValue(pair.Key, out _) : !TryGetValue(pair.Key, out _, context, span)) SetItem(pair.Key, PyNone.Instance);
        }
    }
    internal object SetDefault(object key, object value, MemoryGovernor governor, LythonSourceSpan span)
    {
        var context = ActiveProtocolContext();
        if (_nativeItems is null)
        {
            var bridgeProtocol = PyHashProtocols.NeedsProtocolKey(key);
            var bridgeHash = QueryHash(key, context, span, bridgeProtocol);
            var stored = ResolveBridgeKeyAsync(key, bridgeHash, bridgeProtocol, context, span, asynchronous: false).GetAwaiter().GetResult();
            if (stored is not null && _items.TryGetValue(stored, out var existing)) return existing;
            var before = Count;
            try { _items.AddNew(key, value, bridgeHash); }
            finally { if (Count > before) _version++; }
            return value;
        }
        var protocol = PyHashProtocols.NeedsProtocolKey(key);
        var hash = QueryHash(key, context, span, protocol);
        var index = ResolveNativeKey(key, hash, protocol, context, span);
        if (index >= 0) return _nativeItems[index].Value!;
        AttachMemoryGovernor(governor, span);
        AppendNativeItem(key, value, hash, PyHashProtocols.NeedsProtocolKey(key));
        _version++;
        return value;
    }
    public void AttachMemoryGovernor(MemoryGovernor governor) => AttachMemoryGovernor(governor, null);
    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        if (_memoryGovernor is not null) { _allocationSpan ??= allocationSpan; return; }
        var span = _allocationSpan ?? allocationSpan;
        using var overlap = governor.ReserveTemporary(_nativeItems is null
            ? checked(16L * Count) : _nativeItems.Length == 0 ? 0 : checked(32L + NativeEntryBytes * _nativeItems.Length), span);
        var storage = new PyDict(this, governor, span);
        _nativeItems = storage._nativeItems;
        _nativeCount = storage._nativeCount;
        _nativeProtocolCount = storage._nativeProtocolCount;
        _nativeSequence = storage._nativeSequence;
        _nativeCommittedBytes = storage._nativeCommittedBytes;
        storage._nativeItems = null;
        storage._nativeCount = 0;
        storage._nativeCommittedBytes = 0;
        _items = this;
        _memoryGovernor = governor;
        _allocationSpan = span;
    }

    // Native cleanup resolves the retained entry by identity without guest code.
    internal void RemoveStoredKey(object key)
    {
        if (_nativeItems is null) { if (_items.Remove(key)) _version++; return; }
        var index = FindNativeIdentity(key);
        if (index < 0 && !PyHashProtocols.NeedsProtocolKey(key)) index = FindNativeIndex(key);
        if (index >= 0) { RemoveNativeAt(index); _version++; }
    }
    public bool Remove(object key) => TryRemove(key, out _);
    public bool TryRemove(object key, [MaybeNullWhen(false)] out object value)
    {
        if (_nativeItems is null)
        {
            var bridgeContext = ActiveProtocolContext();
            var bridgeProtocol = PyHashProtocols.NeedsProtocolKey(key);
            var hash = QueryHash(key, bridgeContext, QuerySpan, bridgeProtocol);
            var stored = ResolveBridgeKeyAsync(key, hash, bridgeProtocol, bridgeContext, QuerySpan, asynchronous: false).GetAwaiter().GetResult();
            if (stored is null || !_items.TryGetValue(stored, out value)) { value = PyNone.Instance; return false; }
            if (_items.Remove(stored)) _version++;
            return true;
        }
        var context = ActiveProtocolContext();
        var span = QuerySpan;
        var protocol = PyHashProtocols.NeedsProtocolKey(key);
        var index = ResolveNativeKey(key, QueryHash(key, context, span, protocol), protocol, context, span);
        if (index < 0) { value = PyNone.Instance; return false; }
        value = _nativeItems[index].Value!;
        RemoveNativeAt(index); _version++;
        return true;
    }
    public bool TryRemoveLast([MaybeNullWhen(false)] out object key, [MaybeNullWhen(false)] out object value)
    {
        if (_nativeItems is not null)
        {
            if (_nativeCount == 0) { key = value = PyNone.Instance; return false; }
            var last = _nativeItems[_nativeCount - 1];
            key = last.Key!; value = last.Value!;
            RemoveNativeAt(_nativeCount - 1); _version++;
            return true;
        }
        key = value = PyNone.Instance;
        var found = false;
        foreach (var pair in this) { key = pair.Key; value = pair.Value; found = true; }
        if (found) RemoveStoredKey(key);
        return found;
    }
    public void Clear()
    {
        if (Count == 0) return;
        _items.Clear(); _version++;
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

    internal void DiscardUnpublishedStorage()
    {
        RefundAbortedConstruction(); _items = null!; _nativeItems = null;
    }
    private void RefundAbortedConstruction()
    {
        var release = ReleaseNativeStorage();
        if (release > 0) _memoryGovernor!.Release(release);
    }
    private void EnsureUnmodified(int expectedVersion)
    {
        if (_version != expectedVersion)
            throw new LythonRuntimeException("RuntimeError", "dictionary changed size during iteration", _allocationSpan);
    }
}
