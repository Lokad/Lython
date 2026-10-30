using System.Collections;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Keys and insertion order bridge a live module/class execution namespace.
    // Values come from the actual frame; extra dictionary keys are ordinary values.
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
                var growth = ModuleGrowth(name);
                using var namespaceFunding = _root.MemoryGovernor.ReserveTemporary(growth, _span);
                EnsureNameCapacity(_names.Count + 1);
                var named = new NameEntry(key, name, entryBytes);
                _entries.AddNew(key, named);
                _names.Add(name, named);
                _ownedVariables += OwnedChange(name);
                _root.Variables[name] = value;
                if (!name.StartsWith('<')) _root.CurrentExecutableFrame?.TryStoreLocalOrClosure(name, value);
                namespaceFunding.Commit(growth);
                _root.ClosureChargedVariableCount += checked((int)(growth / ClosureCellSlotBytes));
                entryFunding.Commit(entryBytes);
                _nameBytes += entryBytes;
            }
            else _entries.AddNew(key, value);
        }

        private void EnsureNameCapacity(int count)
        {
            if (count <= _nameCapacity) return;
            // Dictionary prime capacities fit below twice the requested count
            // (with a three-slot minimum). Keep that peak funded before resizing.
            using var funding = _root.MemoryGovernor.ReserveTemporary(32L * Math.Max(3L, 2L * count), _span);
            var capacity = _names.EnsureCapacity(count);
            var growth = 32L * (capacity - _nameCapacity);
            funding.Commit(growth);
            _nameIndexBytes += growth;
            _nameCapacity = capacity;
        }

        private long ModuleGrowth(string name)
            => ClosureCellSlotBytes * (long)Math.Max(0, _ownedVariables + OwnedChange(name) - _root.ClosureChargedVariableCount);

        private int OwnedChange(string name) => _root.Variables.ContainsKey(name) ? 0 : 1;

        private void StoreModuleValue(string name, object value)
        {
            var growth = ModuleGrowth(name);
            using var funding = _root.MemoryGovernor.ReserveTemporary(growth, _span);
            _ownedVariables += OwnedChange(name);
            _root.Variables[name] = value;
            if (!name.StartsWith('<')) _root.CurrentExecutableFrame?.TryStoreLocalOrClosure(name, value);
            funding.Commit(growth);
            _root.ClosureChargedVariableCount += checked((int)(growth / ClosureCellSlotBytes));
        }

        public bool Remove(object key)
        {
            if (!_entries.TryGetValue(key, out var entry)) return false;
            if (entry is NameEntry named)
            {
                if (_root.Variables.Remove(named.Name)) _ownedVariables--;
                if (!named.Name.StartsWith('<')) _root.CurrentExecutableFrame?.TryDeleteLocalOrClosure(named.Name);
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

    internal static PyDict CreateGlobalNamespace(ExecutionContext root, LythonSourceSpan span)
    {
        RetainLocalsForLambda(root);
        ChargeClosureRetention(root, root.MemoryGovernor, span);
        var storage = new GlobalNamespaceStorage(root, span);
        var scope = new PyDict(storage, root.MemoryGovernor, span);
        foreach (var pair in root.Variables)
        {
            scope.SetItem(storage.NameKey(pair.Key), pair.Value);
        }
        if (root.CurrentExecutableFrame is { } frame)
            foreach (var pair in frame.EnumerateLocals()) scope.SetItem(storage.NameKey(pair.Key), pair.Value);
        root.GlobalNamespace = scope;
        root.GlobalNamespaceBacking = storage;
        return scope;
    }

    private static void StoreGlobalNamespaceName(ExecutionContext root, string name, object value)
        => root.GlobalNamespace!.SetItem(root.GlobalNamespaceBacking!.NameKey(name), value);

}
