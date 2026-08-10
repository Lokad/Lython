using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractValueResolver
{
    private static bool TryResolveSequenceConstructorAbstractValue(
        string constructorName,
        CallExpressionSyntax call,
        AbstractState bindings,
        out AbstractValue value)
    {
        if (!StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments))
        {
            value = default;
            return false;
        }

        if (arguments.Positional.Count == 0 && arguments.Keywords.Count == 0)
        {
            value = constructorName == "list"
                ? AbstractValue.List([], call.Span)
                : AbstractValue.Tuple([], call.Span);
            return true;
        }

        if (arguments.Positional.Count + arguments.Keywords.Count != 1 ||
            !arguments.TryGetValue(0, "iterable", out var iterableExpression))
        {
            value = default;
            return false;
        }

        if (StaticBindingEngine.TryGetOrderedUnpackingItems(iterableExpression, bindings, out var items))
        {
            var sequenceItems = items.Select(item => item.WithSpan(call.Span)).ToArray();
            value = constructorName == "list"
                ? AbstractValue.List(sequenceItems, call.Span)
                : AbstractValue.Tuple(sequenceItems, call.Span);
            return true;
        }

        if (constructorName == "list" &&
            StaticBindingEngine.TryGetIterableElementAbstractValue(iterableExpression, bindings, out var itemValue))
        {
            value = AbstractValue.ListOf(itemValue.WithSpan(call.Span), call.Span);
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryResolveLenAbstractValue(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (!StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            arguments.Positional.Count + arguments.Keywords.Count != 1 ||
            !arguments.TryGetValue(0, "value", out var valueExpression) ||
            !TryResolve(valueExpression, bindings, out var target) ||
            !StaticAbstractFacts.IsDefinitelySized(target))
        {
            value = default;
            return false;
        }

        value = AbstractValue.IntegerType(call.Span);
        return true;
    }

    private static bool TryResolveSortedAbstractValue(CallExpressionSyntax call, AbstractState bindings, out AbstractValue value)
    {
        if (!StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            !arguments.TryGetValue(0, "iterable", out var iterableExpression) ||
            !StaticBindingEngine.TryGetIterableElementAbstractValue(iterableExpression, bindings, out var itemValue))
        {
            value = default;
            return false;
        }

        value = AbstractValue.ListOf(itemValue.WithSpan(call.Span), call.Span);
        return true;
    }

    private static bool TryResolveSubscriptAbstractValue(SubscriptExpressionSyntax subscript, AbstractState bindings, out AbstractValue value)
    {
        if (!TryResolve(subscript.Target, bindings, out var target) ||
            StaticAbstractFacts.IsDefinitelyNonSubscriptable(target))
        {
            value = default;
            return false;
        }

        if (StaticAbstractFacts.RequiresIntegerIndex(target))
        {
            if (!TryResolve(subscript.Index, bindings, out var index))
            {
                return TryGetIndexedSequenceValue(target, null, subscript.Span, out value);
            }

            if (StaticAbstractFacts.IsDefinitelyNonIntegerLike(index))
            {
                value = default;
                return false;
            }

            if (StaticAbstractFacts.TryGetNonNegativeInt32(index, out var indexValue))
            {
                return TryGetIndexedSequenceValue(target, indexValue, subscript.Span, out value);
            }

            return TryGetIndexedSequenceValue(target, null, subscript.Span, out value);
        }

        if (TryResolveOpenPyxlSubscriptValue(target, subscript, bindings, out value))
        {
            return true;
        }

        if (target.Kind == AbstractValueKind.Dict &&
            TryResolve(subscript.Index, bindings, out var key))
        {
            var pairs = target.RequirePayload<IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>>();
            foreach (var pair in pairs)
            {
                if (AbstractValue.LiteralValuesEqual(pair.Key, key))
                {
                    value = pair.Value.WithSpan(subscript.Span);
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static bool TryResolveOpenPyxlSubscriptValue(
        AbstractValue target,
        SubscriptExpressionSyntax subscript,
        AbstractState bindings,
        out AbstractValue value)
    {
        if (target.Kind == AbstractValueKind.OpenPyxlWorkbook)
        {
            if (!TryResolve(subscript.Index, bindings, out var index) ||
                index.Kind == AbstractValueKind.Unknown ||
                index.IsStringLike)
            {
                value = AbstractValue.OpenPyxlWorksheet(subscript.Span);
                return true;
            }
        }

        if (target.Kind == AbstractValueKind.OpenPyxlWorksheet &&
            TryResolveKnownString(subscript.Index, bindings, out var reference) &&
            OpenPyxlReferenceFacts.IsCellReference(reference))
        {
            value = AbstractValue.OpenPyxlCell(subscript.Span);
            return true;
        }

        if (target.Kind == AbstractValueKind.OpenPyxlTableCollection)
        {
            if (!TryResolve(subscript.Index, bindings, out var index) ||
                index.Kind == AbstractValueKind.Unknown ||
                index.IsStringLike)
            {
                value = AbstractValue.OpenPyxlTable(subscript.Span);
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryResolveSliceAbstractValue(SliceExpressionSyntax slice, AbstractState bindings, out AbstractValue value)
    {
        if (!TryResolve(slice.Target, bindings, out var target) ||
            StaticAbstractFacts.IsDefinitelyNonSliceable(target) ||
            !AreValidSliceBounds(slice, bindings))
        {
            value = default;
            return false;
        }

        switch (target.Kind)
        {
            case AbstractValueKind.String:
            case AbstractValueKind.StringType:
                value = AbstractValue.StringType(slice.Span);
                return true;
            case AbstractValueKind.Bytes:
            case AbstractValueKind.BytesType:
                value = AbstractValue.BytesType(slice.Span);
                return true;
            case AbstractValueKind.List:
                value = AbstractValue.ListOf(StaticBindingEngine.JoinSequenceItems(target.RequirePayload<IReadOnlyList<AbstractValue>>(), slice.Span), slice.Span);
                return true;
            case AbstractValueKind.ListType:
                value = AbstractValue.ListOf((target.RequirePayload<AbstractValue>()).WithSpan(slice.Span), slice.Span);
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool TryGetIndexedSequenceValue(AbstractValue target, int? index, LythonSourceSpan span, out AbstractValue value)
    {
        switch (target.Kind)
        {
            case AbstractValueKind.String:
            case AbstractValueKind.StringType:
                value = AbstractValue.StringType(span);
                return true;

            case AbstractValueKind.Bytes:
            case AbstractValueKind.BytesType:
                value = AbstractValue.IntegerType(span);
                return true;

            case AbstractValueKind.ListType:
                value = (target.RequirePayload<AbstractValue>()).WithSpan(span);
                return true;

            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
                {
                    var items = target.RequirePayload<IReadOnlyList<AbstractValue>>();
                    if (index.HasValue && index.Value >= 0 && index.Value < items.Count)
                    {
                        value = items[index.Value].WithSpan(span);
                        return true;
                    }

                    if (index is null)
                    {
                        value = StaticBindingEngine.JoinSequenceItems(items, span);
                        return true;
                    }

                    value = default;
                    return false;
                }

            default:
                value = default;
                return false;
        }
    }
}
