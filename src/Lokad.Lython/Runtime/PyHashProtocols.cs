namespace Lokad.Lython.Runtime;

// Contextual key hashing (R13b): user-defined __hash__ slots dispatch with an
// explicit execution context. None slots make classes unhashable. Slot checks
// are pure MRO walks (no guest code), so key validation stays context-free; hash
// *invocation* needs a context. Table probing never dispatches through the
// CLR comparer: protocol keys use precomputed hashes and scan
// with precomputed hashes plus element ==.
internal static class PyHashProtocols
{
    // A slot counts as customized when the first MRO type defining it has
    // bases of its own; the root object type (empty bases) carries defaults.
    // Inherited custom slots count: a subclass without its own __eq__ still
    // follows its base's customization like CPython.
    public static bool HasCustomSlot(PyInstance instance, string name)
    {
        if (!instance.Type.TryLookupInMro(name, 0, out _, out var owner) || owner is null)
        {
            return false;
        }

        return owner.Bases.Count > 0;
    }

    public static bool HasDisabledHash(PyInstance instance)
        => instance.Type.TryLookupInMro("__hash__", 0, out var slot, out _) && slot is PyNone;

    // True for keys whose hashing or probing needs protocol dispatch, checked
    // transitively through tuple-likes (a tuple holding a custom-hashed
    // element hashes contextually). Everything else rides the structural
    // comparer bit-for-bit as before.
    public static bool NeedsProtocolKey(object key) => key switch
    {
        PyInstance instance => HasCustomSlot(instance, "__eq__") || HasCustomSlot(instance, "__hash__"),
        _ when PyTupleLike.TryGetItems(key, out var items) => TupleNeedsProtocol(items),
        _ => false,
    };

    private static bool TupleNeedsProtocol(IReadOnlyList<object> items)
    {
        foreach (var item in items)
        {
            if (NeedsProtocolKey(item))
            {
                return true;
            }
        }

        return false;
    }

    // One normalized hash for public hash(), table buckets and tuple elements.
    public static int GetProtocolHash(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (key is PyInstance instance)
        {
            if (HasDisabledHash(instance))
            {
                throw RuntimeErrors.UnhashableType(key, span);
            }

            if (HasCustomSlot(instance, "__hash__"))
            {
                instance.Type.TryLookupInMro("__hash__", 0, out var rawSlot, out _);
                var hashMember = PyAttributeLookup.BindForInstance(instance, rawSlot!, context, span);

                if (hashMember is not LythonRuntime.ICallable hashCallable)
                {
                    throw new LythonRuntimeException("TypeError", "'" + LythonRuntime.UnboundTypeMethod.PythonTypeName(hashMember, context) + "' object is not callable", span);
                }

                var hashValue = hashCallable.Invoke([], span, context);
                if (!Numbers.PyNumberOps.TryAsInteger(hashValue, out var integerHash))
                {
                    throw new LythonRuntimeException("TypeError", "__hash__ method should return an integer", span);
                }

                return PyValueComparer.Instance.GetHashCode(integerHash);
            }

            return PyValueComparer.Instance.GetHashCode(key);
        }

        if (PyTupleLike.TryGetItems(key, out var items))
        {
            // Unhashable elements surface with the house convention (naming
            // the tuple), matching the structural funnel this replaces.
            try
            {
                return ComputeTupleHash(items, context, span);
            }
            catch (PyUnhashableException)
            {
                throw RuntimeErrors.UnhashableType(key, span);
            }
        }

        return PyValueComparer.Instance.GetHashCode(key);
    }

    // Tuple hashing recurses into element hashes with the same combination as
    // the structural path, so all-builtin tuples hash identically either way;
    // only custom-hashed elements take the protocol branch. Cyclic tuples are
    // rejected by key validation before they can arrive here, with this guard
    // as the backstop.
    private static int ComputeTupleHash(IReadOnlyList<object> items, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        using (PyStructuralGuard.EnterSingle(items, span))
        {
            var hash = new HashCode();
            foreach (var item in items)
            {
                PyStructuralGuard.NoteWork();
                hash.Add(GetProtocolHash(item, context, span));
            }

            return PyValueComparer.NormalizeHash(hash.ToHashCode());
        }
    }

    // Table bucket for one key: guest __hash__ when customized (with
    // unhashability), otherwise the structural comparer. Unhashable
    // builtins surface as UnhashableType naming the key, preserving the
    // structural funnel convention (tuples name the tuple).
    public static int BucketHash(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        try
        {
            return GetProtocolHash(key, context, span);
        }
        catch (PyUnhashableException)
        {
            throw RuntimeErrors.UnhashableType(key, span);
        }
    }

    // Protocol key comparison: identity first (always safe), then element ==.
    public static bool ProtocolKeysEqual(object left, object right, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => ReferenceEquals(left, right) || LythonRuntime.ElementEquals(left, right, context, span);
}

// Guest equality/hash dispatch is explicit. Python id(), copy memoization and
// internal reference caches retain reference-identity semantics instead.
internal sealed class ContextualKeyTable<TValue>
{
    internal sealed class Entry
    {
        public Entry(object key, TValue value)
        {
            Key = key;
            Value = value;
        }

        public object Key { get; }

        public TValue Value { get; set; }
    }

    private readonly Dictionary<int, List<Entry>> _buckets = new();
    private readonly List<Entry> _entries = new();

    public int Count => _entries.Count;

    public IReadOnlyList<Entry> EntriesInOrder => _entries;

    public bool TryGetValue(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span, out TValue value)
    {
        return TryGetValue(key, PyHashProtocols.BucketHash(key, context, span), context, span, out value);
    }

    public bool TryGetValue(object key, int hash, LythonRuntime.ExecutionContext context, LythonSourceSpan span, out TValue value)
    {
        if (_buckets.TryGetValue(hash, out var bucket))
        {
            // Snapshot: guest == may reenter this table (recursive cached calls).
            foreach (var entry in bucket.ToArray())
            {
                if (PyHashProtocols.ProtocolKeysEqual(entry.Key, key, context, span))
                {
                    value = entry.Value;
                    return true;
                }
            }
        }

        value = default!;
        return false;
    }

    // Single hash-plus-scan for get-or-create: first-seen order on adds.
    public Entry GetOrAdd(object key, TValue value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var hash = PyHashProtocols.BucketHash(key, context, span);
        if (!_buckets.TryGetValue(hash, out var bucket))
        {
            bucket = new List<Entry>();
            _buckets[hash] = bucket;
        }
        else
        {
            // Snapshot: guest == may reenter this table (recursive cached calls).
            foreach (var entry in bucket.ToArray())
            {
                if (PyHashProtocols.ProtocolKeysEqual(entry.Key, key, context, span))
                {
                    return entry;
                }
            }
        }

        var added = new Entry(key, value);
        bucket.Add(added);
        _entries.Add(added);
        return added;
    }

    // Single hash-plus-scan get-or-create with a miss-only factory (avoids
    // eager garbage like one empty list per duplicate element).
    public Entry GetOrAdd(object key, Func<TValue> factory, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var hash = PyHashProtocols.BucketHash(key, context, span);
        if (!_buckets.TryGetValue(hash, out var bucket))
        {
            bucket = new List<Entry>();
            _buckets[hash] = bucket;
        }
        else
        {
            // Snapshot: guest == may reenter this table (recursive cached calls).
            foreach (var entry in bucket.ToArray())
            {
                if (PyHashProtocols.ProtocolKeysEqual(entry.Key, key, context, span))
                {
                    return entry;
                }
            }
        }

        var added = new Entry(key, factory());
        bucket.Add(added);
        _entries.Add(added);
        return added;
    }

    // Updates an existing entry in place (no order change); false if missing.
    public bool TrySet(object key, TValue value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var hash = PyHashProtocols.BucketHash(key, context, span);
        if (_buckets.TryGetValue(hash, out var bucket))
        {
            // Snapshot: guest == may reenter this table (recursive cached calls).
            foreach (var entry in bucket.ToArray())
            {
                if (PyHashProtocols.ProtocolKeysEqual(entry.Key, key, context, span))
                {
                    entry.Value = value;
                    return true;
                }
            }
        }

        return false;
    }

    public void Add(object key, TValue value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => Add(key, value, PyHashProtocols.BucketHash(key, context, span));

    // Already-hashed publication and reference removal cannot reenter guest code.
    public Entry Add(object key, TValue value, int hash)
    {
        if (!_buckets.TryGetValue(hash, out var bucket))
        {
            bucket = new List<Entry>();
            _buckets[hash] = bucket;
        }
        var added = new Entry(key, value);
        bucket.Add(added);
        _entries.Add(added);
        return added;
    }

    public void Remove(Entry entry, int hash)
    {
        if (!_buckets.TryGetValue(hash, out var bucket)) return;
        bucket.Remove(entry);
        _entries.Remove(entry);
        if (bucket.Count == 0) _buckets.Remove(hash);
    }

    public bool Remove(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var hash = PyHashProtocols.BucketHash(key, context, span);
        if (!_buckets.TryGetValue(hash, out var bucket))
        {
            return false;
        }

        // Snapshot scan, removal by reference: guest == may reenter (see above).
        foreach (var stored in bucket.ToArray())
        {
            if (PyHashProtocols.ProtocolKeysEqual(stored.Key, key, context, span))
            {
                bucket.Remove(stored);
                _entries.Remove(stored);
                return true;
            }
        }

        return false;
    }
}

internal sealed class ContextualKeySet
{
    private readonly ContextualKeyTable<object?> _table = new();

    public int Count => _table.Count;

    public IEnumerable<object> Keys
    {
        get
        {
            foreach (var entry in _table.EntriesInOrder)
            {
                yield return entry.Key;
            }
        }
    }

    public bool Contains(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => _table.TryGetValue(key, context, span, out _);

    public void Add(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!Contains(key, context, span))
        {
            _table.Add(key, null, context, span);
        }
    }

    public bool Remove(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => _table.Remove(key, context, span);
}
