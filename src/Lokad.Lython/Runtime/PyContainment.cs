using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PyContainment
{
    public static bool Contains(object container, object candidate, LythonSourceSpan span)
    {
        return container switch
        {
            PyRange range => LythonRuntime.RangeContains(range, candidate),
            IPyContainsValue contains => contains.Contains(candidate, span),
            PyString text when PyStringOps.TryAsString(candidate, out var part) => text.Contains(part),
            PyString => throw new LythonRuntimeException("TypeError", "'in <string>' requires string as left operand, not " + RuntimeErrors.OperandTypeName(candidate), span),
            PyBytes haystack => LythonRuntime.ContainsBytesAsync(haystack, candidate, PyStructuralGuard.AmbientContext, span, false).GetAwaiter().GetResult(),
            PyDict dict => dict.ContainsKey(LythonRuntime.ValidateDictionaryKey(candidate, span)),
            PyCounter counter => counter.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _),
            PyDefaultDict defaultDict => defaultDict.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _),
            PyChainMap chainMap => chainMap.ContainsKey(candidate, span),
            PySet set => ContainsInSet(set, candidate, span),
            LythonRuntime.DictKeysView keysView => keysView.Source.ContainsKey(LythonRuntime.ValidateDictionaryKey(candidate, span)),
            LythonRuntime.DictItemsView itemsView => ContainsInDictItems(itemsView, candidate, span),
            ChainMapKeysView chainKeys => chainKeys.Owner.ContainsKey(candidate, span),
            ChainMapValuesView chainValues => ContainsInChainMapValues(chainValues, candidate, span),
            ChainMapItemsView chainItems => ContainsInChainMapItems(chainItems, candidate, span),
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
        // R13b: mapping arms below observe ambient provenance.
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        return container switch
        {
            PyRange range => LythonRuntime.RangeContains(range, candidate),
            IPyContainsValue contains => contains.Contains(candidate, span),
            PyString text when PyStringOps.TryAsString(candidate, out var part) => text.Contains(part),
            PyString => throw new LythonRuntimeException("TypeError", "'in <string>' requires string as left operand, not " + RuntimeErrors.OperandTypeName(candidate), span),
            PyBytes haystack => LythonRuntime.ContainsBytesAsync(haystack, candidate, context, span, false).GetAwaiter().GetResult(),
            PyDict dict => dict.ContainsKey(LythonRuntime.ValidateDictionaryKey(candidate, span)),
            PyCounter counter => counter.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _),
            PyDefaultDict defaultDict => defaultDict.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _),
            PyChainMap chainMap => chainMap.ContainsKey(candidate, span),
            PySet set => ContainsInSet(set, candidate, span),
            LythonRuntime.DictKeysView keysView => keysView.Source.ContainsKey(LythonRuntime.ValidateDictionaryKey(candidate, span)),
            LythonRuntime.DictItemsView itemsView => ContainsInDictItems(itemsView, candidate, span),
            ChainMapKeysView chainKeys => chainKeys.Owner.ContainsKey(candidate, span),
            ChainMapValuesView chainValues => ContainsInChainMapValuesWithProtocols(chainValues, candidate, context, span),
            ChainMapItemsView chainItems => ContainsInChainMapItemsWithProtocols(chainItems, candidate, context, span),
            PyTuple tuple => ContainsInTupleWithProtocols(tuple, candidate, context, span),
            IEnumerable<object> sequence => ContainsInTypedSequenceWithProtocols(sequence, candidate, context, span),
            System.Collections.IEnumerable sequence => ContainsInUntypedSequenceWithProtocols(sequence, candidate, context, span),
            _ => throw RuntimeErrors.ArgumentNotIterable(container, span),
        };
    }

    // Native dictionary/set reads and sequences can suspend through their
    // explicit context. Other arms retain their existing completion behavior.
    public static ValueTask<bool> ContainsWithProtocolsAsync(object container, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (container is PyDict dict)
            return dict.ContainsKeyAsync(LythonRuntime.ValidateDictionaryKey(candidate, span), context, span);
        if (container is PySet set)
            return set.ContainsAsync(LythonRuntime.ValidateDictionaryKey(candidate, span), context, span);
        if (container is LythonRuntime.DictKeysView keys)
            return ContainsInDictKeysAsync(keys, candidate, context, span);
        if (container is LythonRuntime.DictItemsView items)
            return ContainsInDictItemsAsync(items, candidate, context, span);
        // Ambient provenance is only borrowed for each synchronous prefix;
        // returned pending operations carry their explicit context instead.
        using var _ambientScope = PyStructuralGuard.PushAmbient(context, span);
        return container switch
        {
            PyRange range => new ValueTask<bool>(LythonRuntime.RangeContains(range, candidate)),
            IPyContainsValue contains => new ValueTask<bool>(contains.Contains(candidate, span)),
            PyString text when PyStringOps.TryAsString(candidate, out var part) => new ValueTask<bool>(text.Contains(part)),
            PyString => throw new LythonRuntimeException("TypeError", "'in <string>' requires string as left operand, not " + RuntimeErrors.OperandTypeName(candidate), span),
            PyBytes haystack => LythonRuntime.ContainsBytesAsync(haystack, candidate, context, span, true),
            PyCounter counter => new ValueTask<bool>(counter.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _)),
            PyDefaultDict defaultDict => new ValueTask<bool>(defaultDict.TryGetValue(LythonRuntime.ValidateDictionaryKey(candidate, span), out _)),
            PyChainMap chainMap => new ValueTask<bool>(chainMap.ContainsKey(candidate, span)),
            ChainMapKeysView chainKeys => new ValueTask<bool>(chainKeys.Owner.ContainsKey(candidate, span)),
            ChainMapValuesView chainValues => ContainsInChainMapValuesWithProtocolsAsync(chainValues, candidate, context, span),
            ChainMapItemsView chainItems => ContainsInChainMapItemsWithProtocolsAsync(chainItems, candidate, context, span),
            PyTuple tuple => ContainsInTupleWithProtocolsAsync(tuple, candidate, context, span),
            IEnumerable<object> sequence => ContainsInTypedSequenceWithProtocolsAsync(sequence, candidate, context, span),
            System.Collections.IEnumerable sequence => ContainsInUntypedSequenceWithProtocolsAsync(sequence, candidate, context, span),
            _ => throw RuntimeErrors.ArgumentNotIterable(container, span),
        };
    }

    // The shared store hashes even empty queries. Do not prehash structurally:
    // earlier custom hash effects must precede a later unhashable tuple item.
    private static bool ContainsInSet(PySet set, object candidate, LythonSourceSpan span)
    {
        try
        {
            return set.Contains(candidate);
        }
        catch (PyUnhashableException error)
        {
            throw RuntimeErrors.UnhashableType(error.Value, span);
        }
    }

    // ChainMap view membership scans the underlying mappings directly instead
    // of materializing a merged snapshot per check. Keys consult containment
    // (never factories); items stop at the first map holding the key like a
    // subscript read; values test existence. No snapshot is retained.
    private static bool ContainsInChainMapValues(ChainMapValuesView view, object candidate, LythonSourceSpan span)
    {
        foreach (var map in view.Owner.Maps)
        {
            foreach (var value in map.Values)
            {
                PyStructuralGuard.NoteWork();
                if (PyEquality.AreEqual(value, candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsInChainMapItems(ChainMapItemsView view, object candidate, LythonSourceSpan span)
    {
        if (!PyTupleLike.TryGetItems(candidate, out var pair) || pair.Count != 2)
        {
            return false;
        }
        LythonRuntime.ValidateDictionaryKey(pair[0], span);

        foreach (var map in view.Owner.Maps)
        {
            PyStructuralGuard.NoteWork();
            if (map.TryGetValue(pair[0], out var value))
            {
                return PyEquality.AreEqual(value, pair[1]);
            }
        }

        return false;
    }

    private static bool ContainsInChainMapValuesWithProtocols(ChainMapValuesView view, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var map in view.Owner.Maps)
        {
            foreach (var value in map.Values)
            {
                PyStructuralGuard.NoteWork(context, span);
                if (LythonRuntime.MembershipEquals(value, candidate, context, span))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsInChainMapItemsWithProtocols(ChainMapItemsView view, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!PyTupleLike.TryGetItems(candidate, out var pair) || pair.Count != 2)
        {
            return false;
        }
        LythonRuntime.ValidateDictionaryKey(pair[0], span);

        foreach (var map in view.Owner.Maps)
        {
            PyStructuralGuard.NoteWork(context, span);
            if (map.TryGetValue(pair[0], out var value))
            {
                return LythonRuntime.MembershipEquals(value, pair[1], context, span);
            }
        }

        return false;
    }

    private static async ValueTask<bool> ContainsInChainMapValuesWithProtocolsAsync(ChainMapValuesView view, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        foreach (var map in view.Owner.Maps)
        {
            foreach (var value in map.Values)
            {
                PyStructuralGuard.NoteWork(context, span);
                if (await LythonRuntime.MembershipEqualsAsync(value, candidate, context, span).ConfigureAwait(false))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static async ValueTask<bool> ContainsInChainMapItemsWithProtocolsAsync(ChainMapItemsView view, object candidate, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (!PyTupleLike.TryGetItems(candidate, out var pair) || pair.Count != 2)
        {
            return false;
        }
        LythonRuntime.ValidateDictionaryKey(pair[0], span);

        foreach (var map in view.Owner.Maps)
        {
            PyStructuralGuard.NoteWork(context, span);
            if (map.TryGetValue(pair[0], out var value))
            {
                return await LythonRuntime.MembershipEqualsAsync(value, pair[1], context, span).ConfigureAwait(false);
            }
        }

        return false;
    }

    // Tuples are immutable and array-backed: scan by index instead of
    // enumerating through the interface, which boxes an enumerator per
    // membership check.
    private static bool ContainsInTuple(PyTuple tuple, object candidate)
    {
        for (var i = 0; i < tuple.Count; i++)
        {
            PyStructuralGuard.NoteWork();
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
            PyStructuralGuard.NoteWork();
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
            PyStructuralGuard.NoteWork();
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
            PyStructuralGuard.NoteWork(context, span);
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
            PyStructuralGuard.NoteWork(context, span);
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
            PyStructuralGuard.NoteWork(context, span);
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
            PyStructuralGuard.NoteWork(context, span);
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
            PyStructuralGuard.NoteWork(context, span);
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
            PyStructuralGuard.NoteWork(context, span);
            if (await LythonRuntime.MembershipEqualsAsync(LythonRuntime.RuntimeValue(item), candidate, context, span).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }
}
