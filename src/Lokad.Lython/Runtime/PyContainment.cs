using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PyContainment
{
    public static bool Contains(object container, object candidate, LythonSourceSpan span)
    {
        return container switch
        {
            PyRange range => LythonRuntime.RangeContains(range, candidate),
            IPyContainsValue contains => contains.Contains(candidate, span),
            PyString text when PyStringOps.TryAsString(candidate, out var part) => text.Contains(part),
            PyString => throw new LythonRuntimeException("TypeError", "'in <string>' requires string as left operand, not " + RuntimeErrors.OperandTypeName(candidate), span),
            PyBytes haystack => ContainsInBytes(haystack, candidate, span),
            PyDict dict => dict.ContainsKey(LythonRuntime.ValidateDictionaryKey(candidate, span)),
            PyCounter counter => counter.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _),
            PyDefaultDict defaultDict => defaultDict.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _),
            PyChainMap chainMap => chainMap.ContainsKey(candidate, span),
            PySet set => ContainsInSet(set, candidate, span),
            LythonRuntime.DictKeysView keysView => ContainsInValidatedView(keysView, candidate, span),
            LythonRuntime.DictItemsView itemsView => ContainsInValidatedView(itemsView, candidate, span),
            PyTuple tuple => ContainsInTuple(tuple, candidate),
            IEnumerable<object> sequence => ContainsInTypedSequence(sequence, candidate),
            System.Collections.IEnumerable sequence => ContainsInUntypedSequence(sequence, candidate),
            _ => throw RuntimeErrors.ArgumentNotIterable(container, span),
        };
    }

    // Operator membership consults the member __eq__ protocol for linear
    // sequence scans (tuple, list, and generic enumerables); every hash,
    // range, or substring arm below delegates to the same helper as
    // Contains, so this mirror must gain any arm added there.
    public static bool ContainsWithProtocols(object container, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return container switch
        {
            PyRange range => LythonRuntime.RangeContains(range, candidate),
            IPyContainsValue contains => contains.Contains(candidate, span),
            PyString text when PyStringOps.TryAsString(candidate, out var part) => text.Contains(part),
            PyString => throw new LythonRuntimeException("TypeError", "'in <string>' requires string as left operand, not " + RuntimeErrors.OperandTypeName(candidate), span),
            PyBytes haystack => ContainsInBytes(haystack, candidate, span),
            PyDict dict => dict.ContainsKey(LythonRuntime.ValidateDictionaryKey(candidate, span)),
            PyCounter counter => counter.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _),
            PyDefaultDict defaultDict => defaultDict.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _),
            PyChainMap chainMap => chainMap.ContainsKey(candidate, span),
            PySet set => ContainsInSet(set, candidate, span),
            LythonRuntime.DictKeysView keysView => ContainsInValidatedView(keysView, candidate, span),
            LythonRuntime.DictItemsView itemsView => ContainsInValidatedView(itemsView, candidate, span),
            PyTuple tuple => ContainsInTupleWithProtocols(tuple, candidate, context, span),
            IEnumerable<object> sequence => ContainsInTypedSequenceWithProtocols(sequence, candidate, context, span),
            System.Collections.IEnumerable sequence => ContainsInUntypedSequenceWithProtocols(sequence, candidate, context, span),
            _ => throw RuntimeErrors.ArgumentNotIterable(container, span),
        };
    }

    // Async twin of the mirror above: only the three sequence scans suspend
    // (one protocol dispatch per element); every other arm wraps its
    // synchronous helper without allocating a state machine.
    public static ValueTask<bool> ContainsWithProtocolsAsync(object container, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        return container switch
        {
            PyRange range => new ValueTask<bool>(LythonRuntime.RangeContains(range, candidate)),
            IPyContainsValue contains => new ValueTask<bool>(contains.Contains(candidate, span)),
            PyString text when PyStringOps.TryAsString(candidate, out var part) => new ValueTask<bool>(text.Contains(part)),
            PyString => throw new LythonRuntimeException("TypeError", "'in <string>' requires string as left operand, not " + RuntimeErrors.OperandTypeName(candidate), span),
            PyBytes haystack => new ValueTask<bool>(ContainsInBytes(haystack, candidate, span)),
            PyDict dict => new ValueTask<bool>(dict.ContainsKey(LythonRuntime.ValidateDictionaryKey(candidate, span))),
            PyCounter counter => new ValueTask<bool>(counter.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _)),
            PyDefaultDict defaultDict => new ValueTask<bool>(defaultDict.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _)),
            PyChainMap chainMap => new ValueTask<bool>(chainMap.ContainsKey(candidate, span)),
            PySet set => new ValueTask<bool>(ContainsInSet(set, candidate, span)),
            LythonRuntime.DictKeysView keysView => new ValueTask<bool>(ContainsInValidatedView(keysView, candidate, span)),
            LythonRuntime.DictItemsView itemsView => new ValueTask<bool>(ContainsInValidatedView(itemsView, candidate, span)),
            PyTuple tuple => ContainsInTupleWithProtocolsAsync(tuple, candidate, context, span),
            IEnumerable<object> sequence => ContainsInTypedSequenceWithProtocolsAsync(sequence, candidate, context, span),
            System.Collections.IEnumerable sequence => ContainsInUntypedSequenceWithProtocolsAsync(sequence, candidate, context, span),
            _ => throw RuntimeErrors.ArgumentNotIterable(container, span),
        };
    }

    // Bytes membership mirrors CPython: bytes needles match as subsequences
    // (the empty needle is always contained) while integers match single
    // bytes with a range check; anything else names the type.
    private static bool ContainsInBytes(PyBytes haystack, object candidate, LythonSourceSpan span)
    {
        if (candidate is PyBytes needle)
        {
            return haystack.Memory.Span.IndexOf(needle.Memory.Span) >= 0;
        }

        if (candidate is bool flag)
        {
            return haystack.Memory.Span.IndexOf(flag ? (byte)1 : (byte)0) >= 0;
        }

        if (!Numbers.PyNumberOps.TryAsInteger(candidate, out var integer) || integer < 0 || integer > 255)
        {
            if (integer < 0 || integer > 255)
            {
                throw new LythonRuntimeException("ValueError", "byte must be in range(0, 256)", span);
            }

            throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + RuntimeErrors.OperandTypeName(candidate) + "'", span);
        }

        return haystack.Memory.Span.IndexOf((byte)integer) >= 0;
    }

    // Set membership validates hashability through the shared helper so
    // unhashable candidates report the CPython type error instead of leaking
    // the internal control-flow exception. The empty set gets an explicit
    // check because the underlying lookup short-circuits without hashing.
    private static bool ContainsInSet(PySet set, object candidate, LythonSourceSpan span)
    {
        try
        {
            if (set.Count == 0)
            {
                _ = PyValueComparer.Instance.GetHashCode(candidate);
            }

            return set.Contains(candidate);
        }
        catch (PyUnhashableException)
        {
            throw RuntimeErrors.UnhashableType(candidate, span);
        }
    }

    // Dict key/item views hash their candidates like CPython instead of
    // scanning past unhashable ones, so validate first through the shared
    // key helper (which names the inner type for tuples).
    private static bool ContainsInValidatedView(IEnumerable<object> view, object candidate, LythonSourceSpan span)
    {
        LythonRuntime.ValidateDictionaryKey(candidate, span);
        return ContainsInTypedSequence(view, candidate);
    }

    // Tuples are immutable and array-backed: scan by index instead of
    // enumerating through the interface, which boxes an enumerator per
    // membership check.
    private static bool ContainsInTuple(PyTuple tuple, object candidate)
    {
        for (var i = 0; i < tuple.Count; i++)
        {
            if (PyEquality.AreEqual(tuple[i], candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInTypedSequence(IEnumerable<object> sequence, object candidate)
    {
        foreach (var item in sequence)
        {
            if (PyEquality.AreEqual(item, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInUntypedSequence(System.Collections.IEnumerable sequence, object candidate)
    {
        foreach (var item in sequence)
        {
            if (PyEquality.AreEqual(LythonRuntime.RuntimeValue(item), candidate))
            {
                return true;
            }
        }

        return false;
    }

    // Tuple scan consulting the member __eq__ protocol per element, keeping
    // the index walk (no enumerator box).
    private static bool ContainsInTupleWithProtocols(PyTuple tuple, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        for (var i = 0; i < tuple.Count; i++)
        {
            if (LythonRuntime.MembershipEquals(tuple[i], candidate, context, span))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInTypedSequenceWithProtocols(IEnumerable<object> sequence, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var item in sequence)
        {
            if (LythonRuntime.MembershipEquals(item, candidate, context, span))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsInUntypedSequenceWithProtocols(System.Collections.IEnumerable sequence, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var item in sequence)
        {
            if (LythonRuntime.MembershipEquals(LythonRuntime.RuntimeValue(item), candidate, context, span))
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<bool> ContainsInTupleWithProtocolsAsync(PyTuple tuple, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        for (var i = 0; i < tuple.Count; i++)
        {
            if (await LythonRuntime.MembershipEqualsAsync(tuple[i], candidate, context, span).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<bool> ContainsInTypedSequenceWithProtocolsAsync(IEnumerable<object> sequence, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var item in sequence)
        {
            if (await LythonRuntime.MembershipEqualsAsync(item, candidate, context, span).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<bool> ContainsInUntypedSequenceWithProtocolsAsync(System.Collections.IEnumerable sequence, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var item in sequence)
        {
            if (await LythonRuntime.MembershipEqualsAsync(LythonRuntime.RuntimeValue(item), candidate, context, span).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }
}
