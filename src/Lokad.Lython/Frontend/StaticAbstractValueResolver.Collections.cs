namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractValueResolver
{
    private static AbstractValue ResolveAbstractValueList(
        IReadOnlyList<ExpressionSyntax> expressions,
        IReadOnlyList<bool> unpackingFlags,
        LythonSourceSpan span,
        AbstractValueKind kind,
        AbstractState bindings)
    {
        var items = new List<AbstractValue>(expressions.Count);
        for (var i = 0; i < expressions.Count; i++)
        {
            var resolved = ResolveOrUnknown(expressions[i], bindings);
            if (!unpackingFlags[i])
            {
                items.Add(resolved);
                continue;
            }

            switch (resolved.Kind)
            {
                case AbstractValueKind.List:
                case AbstractValueKind.Tuple:
                case AbstractValueKind.Set:
                    items.AddRange(resolved.RequirePayload<IReadOnlyList<AbstractValue>>());
                    break;
                case AbstractValueKind.String:
                    items.AddRange((resolved.RequirePayload<string>()).Select(character =>
                        AbstractValue.String(character.ToString(), expressions[i].Span)));
                    break;
                case AbstractValueKind.Bytes:
                    items.AddRange((resolved.RequirePayload<byte[]>()).Select(value =>
                        AbstractValue.Integer(value.ToString(System.Globalization.CultureInfo.InvariantCulture), expressions[i].Span)));
                    break;
                case AbstractValueKind.Dict:
                    items.AddRange((resolved.RequirePayload<IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>>()).Select(pair => pair.Key));
                    break;
                default:
                    return AbstractValue.Unknown(span);
            }
        }

        return kind switch
        {
            AbstractValueKind.List => AbstractValue.List(items, span),
            AbstractValueKind.Tuple => AbstractValue.Tuple(items, span),
            AbstractValueKind.Set => AbstractValue.Set(items, span),
            _ => throw new InvalidOperationException($"{kind} is not a literal sequence kind.")
        };
    }

    private static AbstractValue ResolveAbstractDictValue(DictLiteralExpressionSyntax dict, AbstractState bindings)
    {
        var pairs = new List<KeyValuePair<AbstractValue, AbstractValue>>(dict.Items.Count);
        foreach (var item in dict.Items)
        {
            if (item is DictionaryUnpackingItemSyntax unpacking)
            {
                var mapping = ResolveOrUnknown(unpacking.Mapping, bindings);
                if (mapping.Kind != AbstractValueKind.Dict)
                {
                    return AbstractValue.Unknown(dict.Span);
                }

                pairs.AddRange(mapping.RequirePayload<IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>>());
                continue;
            }

            pairs.Add(new KeyValuePair<AbstractValue, AbstractValue>(
                ResolveOrUnknown(item.Key, bindings),
                ResolveOrUnknown(item.Value, bindings)));
        }

        return AbstractValue.Dict(pairs, dict.Span);
    }

    private static AbstractValue ResolveListComprehensionValue(ListComprehensionExpressionSyntax listComprehension, AbstractState bindings)
    {
        var comprehensionBindings = StaticBindingEngine.BindComprehensionClauses(listComprehension.Clauses, bindings);
        return AbstractValue.ListOf(
            ResolveOrUnknown(listComprehension.ItemExpression, comprehensionBindings),
            listComprehension.Span);
    }

    private static AbstractValue ResolveSetComprehensionValue(SetComprehensionExpressionSyntax setComprehension, AbstractState bindings)
    {
        var comprehensionBindings = StaticBindingEngine.BindComprehensionClauses(setComprehension.Clauses, bindings);
        return AbstractValue.SetOf(
            ResolveOrUnknown(setComprehension.ItemExpression, comprehensionBindings),
            setComprehension.Span);
    }
}
