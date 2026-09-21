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

    // Protocol key comparison: identity first (always safe), then element ==.
    public static bool ProtocolKeysEqual(object left, object right, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => ReferenceEquals(left, right) || LythonRuntime.ElementEquals(left, right, context, span);
}
