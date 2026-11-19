namespace Lokad.Lython.Frontend;

internal static partial class StaticAbstractValueResolver
{
    private static AbstractValue ResolveAbstractValueList(
        IReadOnlyList<CollectionDisplayItemSyntax> expressions,
        LythonSourceSpan span,
        AbstractValueKind kind,
        AbstractState bindings)
    {
        var items = new List<AbstractValue>(expressions.Count);
        for (var i = 0; i < expressions.Count; i++)
        {
            var resolved = ResolveOrUnknown(expressions[i].Expression, bindings);
            if (!expressions[i].IsUnpacking)
            {
                items.Add(resolved);
                continue;
            }

            switch (resolved.Kind)
            {
                case AbstractValueKind.List:
                case AbstractValueKind.Tuple:
                case AbstractValueKind.Set:
                    items.AddRange(resolved.RequireSequenceItems());
                    break;
                case AbstractValueKind.String:
                    StaticAbstractFacts.AppendTextItems(items, resolved.RequireText(), expressions[i].Span, bindings.AnalysisContext);
                    break;
                case AbstractValueKind.Bytes:
                    items.AddRange((resolved.RequireBytes()).Select(value =>
                        AbstractValue.Integer(value, expressions[i].Span)));
                    break;
                case AbstractValueKind.Dict:
                    items.AddRange((resolved.RequireDictionaryItems()).Select(pair => pair.Key));
                    break;
                default:
                    return AbstractValue.Unknown(span);
            }
        }

        return kind switch
        {
            AbstractValueKind.List => AbstractValue.List(items, span, bindings.AnalysisContext),
            AbstractValueKind.Tuple => AbstractValue.Tuple(items, span, bindings.AnalysisContext),
            AbstractValueKind.Set => AbstractValue.Set(items, span, bindings.AnalysisContext),
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

                pairs.AddRange(mapping.RequireDictionaryItems());
                continue;
            }

            pairs.Add(new KeyValuePair<AbstractValue, AbstractValue>(
                ResolveOrUnknown(item.Key, bindings),
                ResolveOrUnknown(item.Value, bindings)));
        }

        return AbstractValue.Dict(pairs, dict.Span, bindings.AnalysisContext);
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
