using Lokad.Lython.Runtime.Text;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Runtime;

// Sets use the same actual-hash entry authority as dictionaries. Its shell
// estimate covers this wrapper too; values are an internal shared None singleton.
internal sealed class PySet : IEnumerable<object>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPySizedValue
{
    private readonly PyDict _store;
    private int _version;

    public PySet() : this(PyDict.CreateSetStorage(null, null)) { }
    private PySet(PyDict store) => _store = store;
    public PySet(MemoryGovernor governor) : this(governor, null) { }
    public PySet(MemoryGovernor governor, LythonSourceSpan? allocationSpan) : this(PyDict.CreateSetStorage(governor, allocationSpan)) { }
    public PySet(IEnumerable<object> items) : this(items is PySet source ? new PyDict(source._store) : PyDict.CreateSetStorage(null, null))
    {
        if (items is PySet) return;
        try { foreach (var item in items) Add(item); }
        catch { ReleaseUnpublishedStorage(); throw; }
    }
    public PySet(IEnumerable<object> items, MemoryGovernor governor) : this(items, governor, null) { }
    public PySet(IEnumerable<object> items, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(items is PySet source ? new PyDict(source._store, governor, allocationSpan) : PyDict.CreateSetStorage(governor, allocationSpan))
    {
        if (items is PySet) return;
        try
        {
            if (items is ICollection<object> sized) EnsureCapacity(sized.Count);
            foreach (var item in items) Add(item);
        }
        catch { ReleaseUnpublishedStorage(); throw; }
    }
    public PySet(PySet other) : this(new PyDict(other._store)) { }
    public PySet(PySet other, MemoryGovernor governor) : this(other, governor, null) { }
    public PySet(PySet other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        : this(new PyDict(other._store, governor, allocationSpan)) { }

    public int Count => _store.Count;
    public int Length => Count;
    internal long CommittedStorageBytes => _store.CommittedStorageBytes;
    public MemoryGovernor? OwnerMemoryGovernor => _store.OwnerMemoryGovernor;
    public LythonSourceSpan? AllocationSpan => _store.AllocationSpan;
    private Context? ActiveContext => PyStructuralGuard.GuestDispatchSuppressed ? null : PyStructuralGuard.AmbientContext;
    private LythonSourceSpan Span => PyStructuralGuard.AmbientSpan ?? AllocationSpan ?? PyDict.UnknownKeySpan;

    public bool Add(object item)
    {
        if (!_store.AddSetKey(item)) return false;
        _version++; return true;
    }
    public bool Contains(object item) => _store.ContainsKey(item);
    internal ValueTask<bool> ContainsAsync(object item, Context context, LythonSourceSpan span)
        => _store.ContainsKeyAsync(item, context, span);
    public bool Remove(object item)
    {
        if (!_store.Remove(item)) return false;
        _version++; return true;
    }
    public bool TryPop(out object item)
    {
        if (!_store.PopSetKey(out item!)) return false;
        _version++; return true;
    }
    public void Clear()
    {
        if (Count != 0) _version++;
        _store.ClearSetStorage();
    }
    public void AttachMemoryGovernor(MemoryGovernor governor) => _store.AttachMemoryGovernor(governor);
    public void AttachMemoryGovernor(MemoryGovernor governor, LythonSourceSpan? allocationSpan)
        => _store.AttachMemoryGovernor(governor, allocationSpan);
    internal void EnsureCapacity(int targetCount)
        => _store.EnsureNativeCapacity(targetCount, OwnerMemoryGovernor, AllocationSpan);
    internal void ReleaseUnpublishedStorage() => _store.DiscardUnpublishedStorage();

    public void UnionWith(PySet other)
    {
        if (ReferenceEquals(this, other)) return;
        foreach (var entry in other._store.StoredKeys())
        {
            PyStructuralGuard.NoteWork();
            if (_store.AddSetKey(entry.Key, entry.Hash)) _version++;
        }
    }
    public void IntersectWith(PySet other) => Filter(other, keepContained: true);
    public void ExceptWith(PySet other) => Filter(other, keepContained: false);
    private void Filter(PySet other, bool keepContained)
    {
        var context = ActiveContext;
        var span = Span;
        var version = _version;
        // A callback can clear either table. The admitted snapshot owns both
        // retained key references and their existing scalar/payload headroom.
        using var storage = OwnerMemoryGovernor?.ReserveTemporary(_store.KeySnapshotBytes, span);
        var source = _store.StoredKeys().ToArray();
        var remove = new bool[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            context?.CheckExecution(span);
            EnsureUnmodified(version);
            remove[i] = other._store.ContainsStoredKey(source[i].Key, source[i].Hash, context, span) != keepContained;
        }
        EnsureUnmodified(version);
        for (var i = 0; i < source.Length; i++)
        {
            if (!remove[i]) continue;
            if (_store.RemoveSetEntry(source[i].Key, source[i].Hash)) _version++;
        }
    }
    public void SymmetricExceptWith(PySet other)
    {
        if (ReferenceEquals(this, other)) { Clear(); return; }
        using var storage = OwnerMemoryGovernor?.ReserveTemporary(other._store.KeySnapshotBytes, Span);
        var source = other._store.StoredKeys().ToArray();
        foreach (var entry in source)
        {
            PyStructuralGuard.NoteWork();
            if (_store.RemoveStoredHashKey(entry.Key, entry.Hash) || _store.AddSetKey(entry.Key, entry.Hash)) _version++;
        }
    }
    public bool SetEquals(PySet other) => Count == other.Count && IsSubsetOf(other)
        && (!_store.HasProtocolKeys && !other._store.HasProtocolKeys || other.IsSubsetOf(this));
    public bool IsSubsetOf(PySet other)
    {
        if (Count > other.Count) return false;
        var context = ActiveContext;
        var span = Span;
        if (!_store.HasProtocolKeys && !other._store.HasProtocolKeys)
        {
            foreach (var entry in _store.StoredKeys())
            {
                context?.CheckExecution(span);
                if (!other._store.ContainsStoredKey(entry.Key, entry.Hash, context, span)) return false;
            }
            return true;
        }
        using var storage = OwnerMemoryGovernor?.ReserveTemporary(_store.KeySnapshotBytes, span);
        var version = _version;
        var source = _store.StoredKeys().ToArray();
        foreach (var entry in source)
        {
            EnsureUnmodified(version);
            context?.CheckExecution(span);
            var contained = other._store.ContainsStoredKey(entry.Key, entry.Hash, context, span);
            EnsureUnmodified(version);
            if (!contained) return false;
        }
        EnsureUnmodified(version);
        return true;
    }
    internal async ValueTask<bool> IsSubsetOfAsync(PySet other, Context context, LythonSourceSpan span)
    {
        context.CheckExecution(span);
        if (Count > other.Count) return false;
        if (Count == 0 || ReferenceEquals(this, other)) return true;
        if (!_store.HasProtocolKeys && !other._store.HasProtocolKeys)
        {
            using var ambient = PyStructuralGuard.PushAmbient(context, span);
            return IsSubsetOf(other);
        }
        using var storage = context.MemoryGovernor.ReserveTemporary(_store.KeySnapshotBytes, span);
        var version = _version;
        var source = _store.StoredKeys().ToArray();
        foreach (var entry in source)
        {
            EnsureUnmodified(version);
            context.CheckExecution(span);
            var contained = await other._store.ContainsStoredKeyAsync(entry.Key, entry.Hash, context, span).ConfigureAwait(false);
            EnsureUnmodified(version);
            if (!contained) return false;
        }
        EnsureUnmodified(version);
        return true;
    }
    public bool IsProperSubsetOf(PySet other) => Count < other.Count && IsSubsetOf(other);
    public bool IsSupersetOf(PySet other) => other.IsSubsetOf(this);
    public bool IsProperSupersetOf(PySet other) => Count > other.Count && IsSupersetOf(other);
    public bool IsTruthy() => Count != 0;
    public IEnumerable<object> Iterate() => this;
    public PyString RenderPython(PyRenderingContext context) => PyRendering.ToReprPyString(this, context);
    public PyString RenderInterpolated(PyRenderingContext context) => PyRendering.ToReprPyString(this, context);

    public IEnumerator<object> GetEnumerator()
    {
        var version = _version;
        for (var i = 0; ; i++)
        {
            EnsureUnmodified(version);
            if (i == Count) yield break;
            // Retain the set, not an abandoned backing array after clear.
            yield return _store.StoredKeyAt(i);
        }
    }
    private void EnsureUnmodified(int expectedVersion)
    {
        if (expectedVersion != _version)
            throw new LythonRuntimeException("RuntimeError", "Set changed size during iteration", AllocationSpan);
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
