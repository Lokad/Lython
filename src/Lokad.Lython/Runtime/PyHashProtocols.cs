using System.Numerics;

namespace Lokad.Lython.Runtime;

// Contextual key hashing (R13b): user-defined __hash__ slots dispatch with an
// explicit execution context, and classes that customize __eq__ without
// __hash__ are unhashable like CPython. Slot *presence* checks are pure MRO
// walks (no guest code), so key validation stays context-free; only hash
// *invocation* needs a context. Table probing never dispatches through the
// CLR comparer: protocol keys bypass Dictionary/HashSet probing and scan
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

    public static bool IsEqWithoutHash(PyInstance instance)
        => HasCustomSlot(instance, "__eq__") && !HasCustomSlot(instance, "__hash__");

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

    // Guest-visible hash payload: dispatches custom __hash__ (validating the
    // integer like CPython), recurses through tuple-likes, rejects
    // eq-without-hash as unhashable, and delegates everything else to the
    // structural comparer so builtin hashes never drift.
    public static BigInteger GetProtocolHashValue(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (key is PyInstance instance)
        {
            if (IsEqWithoutHash(instance))
            {
                throw RuntimeErrors.UnhashableType(key, span);
            }

            if (HasCustomSlot(instance, "__hash__"))
            {
                // An explicit __hash__ = None marks the type unhashable like
                // CPython; read the raw MRO slot since attribute lookup may
                // not surface None values.
                if (instance.Type.TryLookupInMro("__hash__", 0, out var rawSlot, out _) &&
                    ReferenceEquals(rawSlot, PyNone.Instance))
                {
                    throw RuntimeErrors.UnhashableType(key, span);
                }

                if (!instance.TryGetAttribute("__hash__", context, span, out var hashMember) || hashMember is null)
                {
                    return new BigInteger(PyValueComparer.Instance.GetHashCode(key));
                }

                if (hashMember is not LythonRuntime.ICallable hashCallable)
                {
                    throw new LythonRuntimeException("TypeError", "'" + LythonRuntime.UnboundTypeMethod.PythonTypeName(hashMember, context) + "' object is not callable", span);
                }

                var hashValue = hashCallable.Invoke([], span, context);
                if (!Numbers.PyNumberOps.TryAsInteger(hashValue, out var integerHash))
                {
                    throw new LythonRuntimeException("TypeError", "__hash__ method should return an integer", span);
                }

                return integerHash;
            }

            return new BigInteger(PyValueComparer.Instance.GetHashCode(key));
        }

        if (PyTupleLike.TryGetItems(key, out var items))
        {
            // Unhashable elements surface with the house convention (naming
            // the tuple), matching the structural funnel this replaces.
            try
            {
                return new BigInteger(ComputeTupleHash(items, context, span));
            }
            catch (PyUnhashableException)
            {
                throw RuntimeErrors.UnhashableType(key, span);
            }
        }

        return new BigInteger(PyValueComparer.Instance.GetHashCode(key));
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
                hash.Add(GetProtocolHashInt(item, context, span));
            }

            return hash.ToHashCode();
        }
    }

    // Table-grade 32-bit hash: small magnitudes keep their exact comparer
    // value; oversized magnitudes fold to the low 32 bits with CPython's
    // -1 to -2 reservation.
    public static int GetProtocolHash(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // Mask first so the uint conversion below is always in range (the
        // direct BigInteger-to-int32 conversion throws on overflow, including
        // for negative combined hashes); the unchecked reinterpretation to
        // int32 never throws.
        var raw = GetProtocolHashValue(key, context, span);
        var masked = raw & uint.MaxValue;
        var folded = unchecked((int)(uint)masked);
        return folded == -1 ? -2 : folded;
    }

    private static int GetProtocolHashInt(object key, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        // Nested element hashes share the table folding; -1 reservation keeps
        // combined values consistent with standalone table hashes.
        if (key is PyInstance || PyTupleLike.TryGetItems(key, out _))
        {
            return GetProtocolHash(key, context, span);
        }

        return PyValueComparer.Instance.GetHashCode(key);
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

// N12 inventory: module-local object-key tables by purpose.
//
// Python-equality tables (guest __hash__/__eq__ via the policy below):
// - statistics mode/multimode frequency counts (first-seen order decides ties);
// - difflib b2j/bjunk/bpopular/fullBCount/available (element identity decides matches);
// - functools cache keys (tuple graphs; ownership transaction stays N08);
// - core dict/set protocol side lists (lookup/unhashability; N10 owns the redesign).
//
// Identity tables (must never acquire Python equality semantics):
// - id() registry (ConditionalWeakTable keyed by live reference);
// - runtime member caches (ReferenceEquals on the cached target);
// - reclamation pool registrations (WeakReference owner handles);
// - PyValueComparer structural paths (comparer callbacks, hashing, sorts run
//   with guest dispatch suppressed);
// - memoization keyed by object identity (must stay reference-based).
//
// Shared contextual key policy for the equality tables above: hash-bucketed
// side storage with guest __hash__ precomputed once per operation plus
// element == scans (never through CLR comparer callbacks, mirroring the core
// dict/set side lists). First-seen insertion order is preserved for tie
// breaks. Callers own growth accounting (temporaries for scratch, durable
// coupons for retained graphs) exactly like the Dictionary/HashSet versions
// these replace; buckets add no new rates.
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
        var hash = PyHashProtocols.BucketHash(key, context, span);
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
    {
        var hash = PyHashProtocols.BucketHash(key, context, span);
        if (!_buckets.TryGetValue(hash, out var bucket))
        {
            bucket = new List<Entry>();
            _buckets[hash] = bucket;
        }

        var added = new Entry(key, value);
        bucket.Add(added);
        _entries.Add(added);
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
