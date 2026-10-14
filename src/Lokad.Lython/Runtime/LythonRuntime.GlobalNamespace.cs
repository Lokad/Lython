using System.Collections;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Only keys and insertion order live here. String-key values always come
    // from the actual module namespace; extra dictionary keys keep ordinary values.
    internal sealed class GlobalNamespaceStorage : IPyDictStorage
    {
        // Bridge covers the adapter, lookup-table shell and fixed array headers;
        // entries cover the record and decoded string, plus its UTF-16 payload.
        private const long BridgeBytes = 256, NameEntryBytes = 80;
        private readonly ExecutionContext _root;
        private readonly LythonSourceSpan _span;
        private IPyDictStorage _entries;
        private Dictionary<string, NameEntry> _names = new(StringComparer.Ordinal);
        private int _nameCapacity;
        private long _nameIndexBytes;
        private long _nameBytes;
        private int _ownedVariables;
        private sealed record NameEntry(object Key, string Name, long Bytes)
        {
        }


        internal GlobalNamespaceStorage(ExecutionContext root, LythonSourceSpan span)
        {
            root.MemoryGovernor.Reserve(BridgeBytes, span);
            root.MemoryGovernor.Commit(BridgeBytes);
            _root = root;
            _span = span;
            _entries = PyDictStorage.Create(0, root.MemoryGovernor, span);
            _ownedVariables = CountOwnedModuleVariables(root);
        }

        public int Count => _entries.Count;
        public IEnumerable<object> Keys => _entries.Keys;
        public IEnumerable<object> Values => this.Select(pair => pair.Value);
        public long CommittedBytes => BridgeBytes + _entries.CommittedBytes + _nameBytes + _nameIndexBytes;

        internal object NameKey(string name)
        {
            if (_names.TryGetValue(name, out var named)) return named.Key;
            var key = PyString.FromString(name, _root.MemoryGovernor, _span);
            _root.Services.State.CallTemporaries.TrackFreshString(key, _span);
            return key;
        }

        internal bool TryGetNameKey(string name, [MaybeNullWhen(false)] out object key)
        {
            if (_names.TryGetValue(name, out var named)) { key = named.Key; return true; }
            key = null;
            return false;
        }

        public bool TryGetValue(object key, [MaybeNullWhen(false)] out object value)
        {
            if (_entries.TryGetValue(key, out var entry))
            {
                if (entry is NameEntry named) return _root.Variables.TryGetValue(named.Name, out value);
                value = entry;
                return true;
            }
            value = null;
            return false;
        }

        public object GetRequired(object key)
            => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        public bool ContainsKey(object key) => TryGetValue(key, out _);

        public bool SetItem(object key, object value)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing is NameEntry named) StoreModuleValue(named.Name, value);
                else _ = _entries.SetItem(key, value);
                return false;
            }
            AddNew(key, value);
            return true;
        }

        public bool TrySetExisting(object key, object value)
        {
            if (!_entries.ContainsKey(key)) return false;
            _ = SetItem(key, value);
            return true;
        }

        public void AddNew(object key, object value)
        {
            var textKey = key as PyString;
            // Fund the decoded name, metadata record and name lookup entry.
            var entryBytes = textKey is null ? 0 : NameEntryBytes + 2L * System.Text.Encoding.UTF8.GetCharCount(textKey.Utf8Bytes.Span);
            using var entryFunding = _root.MemoryGovernor.ReserveTemporary(entryBytes, _span);
            var name = textKey?.ToString();
            _entries = PyDictStorage.EnsureCapacity(_entries, Count + 1, _root.MemoryGovernor, _span);
            if (name is not null)
            {
                // All funding checks precede changing the visible module binding.
                var growth = ModuleGrowth(name, value);
                using var namespaceFunding = _root.MemoryGovernor.ReserveTemporary(growth, _span);
                EnsureNameCapacity(_names.Count + 1);
                var named = new NameEntry(key, name, entryBytes);
                _entries.AddNew(key, named);
                _names.Add(name, named);
                _ownedVariables += OwnedChange(name, value);
                _root.Variables[name] = value;
                if (!name.StartsWith('<')) _root.CurrentExecutableFrame?.TryStoreLocalOrClosure(name, value);
                namespaceFunding.Dispose();
                CommitModuleGrowth(growth);
                entryFunding.Dispose();
                _root.MemoryGovernor.Reserve(entryBytes, _span);
                _root.MemoryGovernor.Commit(entryBytes);
                _nameBytes += entryBytes;
            }
            else _entries.AddNew(key, value);
        }

        private void EnsureNameCapacity(int count)
        {
            if (count <= _nameCapacity) return;
            // Dictionary prime capacities fit below twice the requested count
            // (with a three-slot minimum). Keep that peak funded before resizing.
            using var funding = _root.MemoryGovernor.ReserveTemporary(32L * (Math.Max(3L, 2L * count) - _nameCapacity), _span);
            var capacity = _names.EnsureCapacity(count);
            var growth = 32L * (capacity - _nameCapacity);
            funding.Dispose();
            _root.MemoryGovernor.Reserve(growth, _span);
            _root.MemoryGovernor.Commit(growth);
            _nameIndexBytes += growth;
            _nameCapacity = capacity;
        }

        private long ModuleGrowth(string name, object value)
            => ClosureCellSlotBytes * (long)Math.Max(0, _ownedVariables + OwnedChange(name, value) - _root.ClosureChargedVariableCount);

        private int OwnedChange(string name, object value)
        {
            var change = 0;
            var shared = _root.Services.State.BuiltinVariables.TryGetValue(name, out var builtin);
            if (_root.Variables.TryGetValue(name, out var previous) && (!shared || !ReferenceEquals(previous, builtin))) change--;
            if (!shared || !ReferenceEquals(value, builtin)) change++;
            return change;
        }

        private void CommitModuleGrowth(long growth)
        {
            if (growth == 0) return;
            _root.MemoryGovernor.Reserve(growth, _span);
            _root.MemoryGovernor.Commit(growth);
            _root.ClosureChargedVariableCount += checked((int)(growth / ClosureCellSlotBytes));
        }

        private void StoreModuleValue(string name, object value)
        {
            var growth = ModuleGrowth(name, value);
            using var funding = _root.MemoryGovernor.ReserveTemporary(growth, _span);
            _ownedVariables += OwnedChange(name, value);
            _root.Variables[name] = value;
            if (!name.StartsWith('<')) _root.CurrentExecutableFrame?.TryStoreLocalOrClosure(name, value);
            funding.Dispose();
            CommitModuleGrowth(growth);
        }

        public bool Remove(object key)
        {
            if (!_entries.TryGetValue(key, out var entry)) return false;
            if (entry is NameEntry named)
            {
                if (_root.Variables.TryGetValue(named.Name, out var previous) &&
                    (!_root.Services.State.BuiltinVariables.TryGetValue(named.Name, out var shared) || !ReferenceEquals(previous, shared)))
                    _ownedVariables--;
                _root.Variables.Remove(named.Name);
                if (!named.Name.StartsWith('<')) _root.CurrentExecutableFrame?.TryDeleteLocalOrClosure(named.Name);
                if (_root.Services.State.BuiltinVariables.TryGetValue(named.Name, out var builtin))
                    _root.Variables[named.Name] = builtin;
                _names.Remove(named.Name);
                _root.MemoryGovernor.Release(named.Bytes);
                _nameBytes -= named.Bytes;
            }
            return _entries.Remove(key);
        }

        public void Clear()
        {
            using var scratch = _root.MemoryGovernor.ReserveTemporary(8L * Count, _span);
            foreach (var key in _entries.Keys.ToArray()) _ = Remove(key);
            var released = _entries.ReleaseCommittedBytes();
            if (released != 0) _root.MemoryGovernor.Release(released);
            _entries = PyDictStorage.Create(0, _root.MemoryGovernor, _span);
            _root.MemoryGovernor.Release(_nameIndexBytes);
            _names = new(StringComparer.Ordinal);
            _nameIndexBytes = 0;
            _nameCapacity = 0;
        }

        public IPyDictStorage Clone() => PyDictStorage.Create(this, null);
        public long ReleaseCommittedBytes() => throw new InvalidOperationException("A live namespace keeps its bridge storage.");
        public IEnumerator<KeyValuePair<object, object>> GetEnumerator()
        {
            foreach (var pair in _entries)
                if (TryGetValue(pair.Key, out var value)) yield return new(pair.Key, value);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static PyDict CreateGlobalNamespace(ExecutionContext root, LythonSourceSpan span)
    {
        RetainLocalsForLambda(root);
        _ = ChargeClosureRetention(root, root.MemoryGovernor, span);
        var storage = new GlobalNamespaceStorage(root, span);
        var scope = new PyDict(storage, root.MemoryGovernor, span);
        // Register the fresh bridge before seeding. A failed seed stays owned
        // by the reclamation pool and is never published as the module view.
        root.Services.State.CallTemporaries.TrackFreshMutable(scope, scope.CommittedStorageBytes, span);
        foreach (var pair in root.Variables)
        {
            if (!root.Services.State.BuiltinVariables.TryGetValue(pair.Key, out var builtin) || !ReferenceEquals(pair.Value, builtin) ||
                root.ModuleBuiltinAliases?.Contains(pair.Key) == true)
                scope.SetItem(storage.NameKey(pair.Key), pair.Value);
        }
        if (root.CurrentExecutableFrame is { } frame)
            foreach (var pair in frame.EnumerateLocals()) scope.SetItem(storage.NameKey(pair.Key), pair.Value);
        root.GlobalNamespace = scope;
        root.GlobalNamespaceBacking = storage;
        root.ModuleBuiltinAliases = null;
        root.ModuleBuiltinAliasCapacity = 0;
        root.MemoryGovernor.Release(root.ModuleBuiltinAliasBytes);
        root.ModuleBuiltinAliasBytes = 0;
        return scope;
    }

    private static void StoreGlobalNamespaceName(ExecutionContext root, string name, object value)
        => root.GlobalNamespace!.SetItem(root.GlobalNamespaceBacking!.NameKey(name), value);

    // Before globals() exists, the module's variables also contain inherited
    // builtins. Record explicit stores of the identical builtin so the live
    // dictionary can distinguish an actual module binding from that fallback.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void NoteModuleBuiltinAlias(ExecutionContext context, string name, object value, LythonSourceSpan span)
    {
        if (context.ParentContext is not null || context.IsClassBody || context.GlobalNamespace is not null) return;
        if (value is not (ICallable or PyType or PyEllipsis or PyNotImplemented))
        {
            context.ModuleBuiltinAliases?.Remove(name);
            return;
        }
        NoteCallableModuleBuiltinAlias(context, name, value, span);
    }

    private static void NoteCallableModuleBuiltinAlias(ExecutionContext context, string name, object value, LythonSourceSpan span)
    {
        if (!context.Services.State.BuiltinVariables.TryGetValue(name, out var builtin) || !ReferenceEquals(value, builtin))
        {
            context.ModuleBuiltinAliases?.Remove(name);
            return;
        }
        if (context.ModuleBuiltinAliases?.Contains(name) == true) return;
        var count = (context.ModuleBuiltinAliases?.Count ?? 0) + 1;
        var shell = context.ModuleBuiltinAliases is null ? 128L : 0;
        using var funding = context.MemoryGovernor.ReserveTemporary(shell +
            32L * Math.Max(0L, Math.Max(3L, 2L * count) - context.ModuleBuiltinAliasCapacity), span);
        var aliases = context.ModuleBuiltinAliases ?? new HashSet<string>(StringComparer.Ordinal);
        var capacity = aliases.EnsureCapacity(count);
        var charge = shell + 32L * (capacity - context.ModuleBuiltinAliasCapacity);
        funding.Dispose();
        context.MemoryGovernor.Reserve(charge, span);
        context.MemoryGovernor.Commit(charge);
        context.ModuleBuiltinAliasBytes += charge;
        context.ModuleBuiltinAliasCapacity = capacity;
        context.ModuleBuiltinAliases = aliases;
        aliases.Add(name);
    }
}
