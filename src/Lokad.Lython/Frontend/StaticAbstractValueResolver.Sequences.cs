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
            value = new AbstractValue(
                constructorName == "list" ? AbstractValueKind.List : AbstractValueKind.Tuple,
                Array.Empty<AbstractValue>(),
                call.Span);
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
            value = new AbstractValue(
                constructorName == "list" ? AbstractValueKind.List : AbstractValueKind.Tuple,
                items.Select(item => item.WithSpan(call.Span)).ToArray(),
                call.Span);
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
            var pairs = (IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)target.Value;
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
            IsOpenPyxlCellReference(reference))
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

    private static bool IsOpenPyxlCellReference(string text)
    {
        var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var index = 0;
        while (index < normalized.Length && char.IsLetter(normalized[index]))
        {
            index++;
        }

        return index > 0 &&
            index < normalized.Length &&
            normalized.Skip(index).All(char.IsDigit);
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
                value = AbstractValue.ListOf(StaticBindingEngine.JoinSequenceItems((IReadOnlyList<AbstractValue>)target.Value, slice.Span), slice.Span);
                return true;
            case AbstractValueKind.ListType:
                value = AbstractValue.ListOf(((AbstractValue)target.Value).WithSpan(slice.Span), slice.Span);
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
                value = ((AbstractValue)target.Value).WithSpan(span);
                return true;

            case AbstractValueKind.List:
            case AbstractValueKind.Tuple:
                {
                    var items = (IReadOnlyList<AbstractValue>)target.Value;
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
