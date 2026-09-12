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
            IEnumerable<object> sequence => ContainsInTypedSequence(sequence, candidate),
            System.Collections.IEnumerable sequence => ContainsInUntypedSequence(sequence, candidate),
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
}
