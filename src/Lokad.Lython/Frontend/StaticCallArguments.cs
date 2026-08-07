namespace Lokad.Lython.Frontend;

internal readonly record struct ConcreteCallArguments(
    IReadOnlyList<ExpressionSyntax> Positional,
    IReadOnlyDictionary<string, ExpressionSyntax> Keywords,
    IReadOnlyList<AbstractValue?>? PositionalValues,
    IReadOnlyDictionary<string, AbstractValue>? KeywordValues)
{
    public ConcreteCallArguments(
        IReadOnlyList<ExpressionSyntax> Positional,
        IReadOnlyDictionary<string, ExpressionSyntax> Keywords)
        : this(Positional, Keywords, null, null)
    {
    }

    public ConcreteCallArguments(
        IReadOnlyList<ExpressionSyntax> Positional,
        IReadOnlyDictionary<string, ExpressionSyntax> Keywords,
        IReadOnlyList<AbstractValue?>? PositionalValues)
        : this(Positional, Keywords, PositionalValues, null)
    {
    }

    public bool TryGetValue(int position, string keyword, [MaybeNullWhen(false)] out ExpressionSyntax expression)
    {
        if (position < Positional.Count)
        {
            expression = Positional[position];
            return true;
        }

        return Keywords.TryGetValue(keyword, out expression);
    }

    public AbstractValue ResolvePositionalValue(int position, AbstractState bindings)
    {
        if (PositionalValues is not null &&
            position < PositionalValues.Count &&
            PositionalValues[position].HasValue)
        {
            return PositionalValues[position].RequireNotNull();
        }

        return StaticAbstractValueResolver.ResolveOrUnknown(Positional[position], bindings);
    }

    public AbstractValue ResolveKeywordValue(string keyword, AbstractState bindings)
    {
        if (KeywordValues is not null &&
            KeywordValues.TryGetValue(keyword, out var value))
        {
            return value;
        }

        return StaticAbstractValueResolver.ResolveOrUnknown(Keywords[keyword], bindings);
    }

    public bool TryResolveValue(int position, string keyword, AbstractState bindings, out AbstractValue value)
    {
        if (position < Positional.Count)
        {
            value = ResolvePositionalValue(position, bindings);
            return true;
        }

        if (Keywords.ContainsKey(keyword))
        {
            value = ResolveKeywordValue(keyword, bindings);
            return true;
        }

        value = default;
        return false;
    }
}

internal static class StaticCallArguments
{
    public static bool TryGetConcreteArguments(CallExpressionSyntax call, out ConcreteCallArguments arguments)
    {
        var positional = new List<ExpressionSyntax>(call.Arguments.Count);
        var keywords = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);

        foreach (var argument in call.Arguments)
        {
            if (argument.Kind is CallArgumentKind.StarredList or CallArgumentKind.StarredDictionary)
            {
                arguments = default;
                return false;
            }

            if (argument.Kind == CallArgumentKind.Keyword)
            {
                if (argument.Name is null)
                {
                    arguments = default;
                    return false;
                }

                keywords[argument.Name] = argument.Expression;
            }
            else
            {
                positional.Add(argument.Expression);
            }
        }

        arguments = new ConcreteCallArguments(positional, keywords);
        return true;
    }

    public static bool TryGetConcreteArguments(CallExpressionSyntax call, AbstractState bindings, out ConcreteCallArguments arguments)
    {
        var positional = new List<ExpressionSyntax>(call.Arguments.Count);
        var positionalValues = new List<AbstractValue?>(call.Arguments.Count);
        var keywords = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        var keywordValues = new Dictionary<string, AbstractValue>(StringComparer.Ordinal);

        foreach (var argument in call.Arguments)
        {
            switch (argument.Kind)
            {
                case CallArgumentKind.Positional:
                    positional.Add(argument.Expression);
                    positionalValues.Add(null);
                    break;

                case CallArgumentKind.Keyword:
                    if (argument.Name is null)
                    {
                        arguments = default;
                        return false;
                    }

                    keywords[argument.Name] = argument.Expression;
                    keywordValues.Remove(argument.Name);
                    break;

                case CallArgumentKind.StarredList:
                    if (!TryAppendStarredList(argument.Expression, bindings, positional, positionalValues))
                    {
                        arguments = default;
                        return false;
                    }

                    break;

                case CallArgumentKind.StarredDictionary:
                    if (!TryAppendStarredDictionary(argument.Expression, bindings, keywords, keywordValues))
                    {
                        arguments = default;
                        return false;
                    }

                    break;

                default:
                    arguments = default;
                    return false;
            }
        }

        arguments = new ConcreteCallArguments(positional, keywords, positionalValues, keywordValues);
        return true;
    }

    private static bool TryAppendStarredList(
        ExpressionSyntax expression,
        AbstractState bindings,
        List<ExpressionSyntax> positional,
        List<AbstractValue?> positionalValues)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        switch (expression)
        {
            case ListLiteralExpressionSyntax list:
                foreach (var item in list.Items)
                {
                    positional.Add(item);
                    positionalValues.Add(null);
                }

                return true;

            case TupleLiteralExpressionSyntax tuple:
                foreach (var item in tuple.Items)
                {
                    positional.Add(item);
                    positionalValues.Add(null);
                }

                return true;
        }

        if (!StaticBindingEngine.TryGetOrderedUnpackingItems(expression, bindings, out var items))
        {
            return false;
        }

        foreach (var item in items)
        {
            positional.Add(expression);
            positionalValues.Add(item);
        }

        return true;
    }

    private static bool TryAppendStarredDictionary(
        ExpressionSyntax expression,
        AbstractState bindings,
        Dictionary<string, ExpressionSyntax> keywords,
        Dictionary<string, AbstractValue> keywordValues)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Inner;
        }

        if (expression is DictLiteralExpressionSyntax dict)
        {
            foreach (var item in dict.Items)
            {
                if (item.IsUnpacking)
                {
                    return false;
                }

                if (!StaticAbstractValueResolver.TryResolveKnownString(item.Key, bindings, out var key))
                {
                    return false;
                }

                keywords[key] = item.Value;
                keywordValues.Remove(key);
            }

            return true;
        }

        if (!StaticAbstractValueResolver.TryResolve(expression, bindings, out var value) ||
            value.Kind != AbstractValueKind.Dict)
        {
            return false;
        }

        foreach (var pair in (IReadOnlyList<KeyValuePair<AbstractValue, AbstractValue>>)value.Value)
        {
            if (pair.Key.Kind != AbstractValueKind.String)
            {
                return false;
            }

            var key = (string)pair.Key.Value;
            keywords[key] = expression;
            keywordValues[key] = pair.Value;
        }

        return true;
    }
}
