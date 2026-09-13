using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class PyDict : IEnumerable<KeyValuePair<object, object>>, IPyTruthyValue, IPyIterableValue, IPyRenderableValue, IPyGovernedValue, IPySizedValue
{
    private IPyDictStorage _items;
    private MemoryGovernor? _memoryGovernor;
    private LythonSourceSpan? _allocationSpan;
    private int _version;

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
    }

    public PyDict(PyDict other, MemoryGovernor governor) : this(other, governor, null) { }

    public PyDict(PyDict other, MemoryGovernor governor, LythonSourceSpan? allocationSpan)
    {
        _memoryGovernor = governor;
        _allocationSpan = allocationSpan;
        _items = PyDictStorage.Create(other._items, governor, allocationSpan);
    }

    public int Count => _items.Count;

    // Current committed backing charges, for pooled owners that release them
    // if this dictionary is dropped without wholesale storage replacement.
    internal long CommittedStorageBytes => _items.CommittedBytes;

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

        value = PyNone.Instance;
        return false;
    }

    public bool ContainsKey(object key) => _items.ContainsKey(ToStorageKey(key));

    public void SetItem(object key, object value)
    {
        var storageKey = ToStorageKey(key);
        var storageValue = ToStorageValue(value);
        if (_items.TrySetExisting(storageKey, storageValue))
        {
            return;
        }

        _items = PyDictStorage.EnsureCapacity(_items, Count + 1, _memoryGovernor, _allocationSpan);
        _items.AddNew(storageKey, storageValue);
        _version++;
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
        if (!_items.Remove(ToStorageKey(key)))
        {
            return false;
        }

        _version++;
        return true;
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
            var released = _items.ReleaseCommittedBytes();
            if (released > 0)
            {
                _memoryGovernor.Release(released);
            }

            _items = PyDictStorage.Create(0, _memoryGovernor, _allocationSpan);
            ChargeReclamationPool.NotifyStorageReplaced(this, CommittedStorageBytes);
            return;
        }

        _items.Clear();
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
