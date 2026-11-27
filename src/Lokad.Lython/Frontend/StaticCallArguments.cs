using System.Collections.Frozen;

namespace Lokad.Lython.Frontend;

internal readonly struct ConcreteCallArguments
{
    private readonly IReadOnlyList<CallArgumentSyntax>? _source;
    private readonly List<ExpressionSyntax>? _expanded;
    private readonly List<AbstractValue?>? _positionalValues;
    private readonly Dictionary<string, AbstractValue>? _keywordValues;

    internal ConcreteCallArguments(IReadOnlyList<CallArgumentSyntax> source, int positionalCount,
        IReadOnlyDictionary<string, ExpressionSyntax> keywords)
    {
        _source = source;
        PositionalCount = positionalCount;
        Keywords = keywords;
    }

    internal ConcreteCallArguments(List<ExpressionSyntax> positional, Dictionary<string, ExpressionSyntax> keywords,
        List<AbstractValue?> positionalValues, Dictionary<string, AbstractValue> keywordValues)
    {
        _expanded = positional;
        _positionalValues = positionalValues;
        _keywordValues = keywordValues;
        PositionalCount = positional.Count;
        Keywords = keywords;
    }

    public int PositionalCount { get; }
    public IReadOnlyDictionary<string, ExpressionSyntax> Keywords { get; }

    public ExpressionSyntax GetPositional(int position)
    {
        System.Diagnostics.Debug.Assert((uint)position < (uint)PositionalCount);
        return _source is not null ? _source[position].Expression : _expanded![position];
    }

    public bool TryGetValue(int position, string keyword, [MaybeNullWhen(false)] out ExpressionSyntax expression)
    {
        if (position < PositionalCount)
        {
            expression = GetPositional(position);
            return true;
        }

        return Keywords.TryGetValue(keyword, out expression);
    }

    public AbstractValue ResolvePositionalValue(int position, AbstractState bindings)
    {
        if (_positionalValues is not null && _positionalValues[position] is { } value)
        {
            return value;
        }

        return StaticAbstractValueResolver.ResolveOrUnknown(GetPositional(position), bindings);
    }

    public AbstractValue ResolveKeywordValue(string keyword, AbstractState bindings)
    {
        if (_keywordValues is not null &&
            _keywordValues.TryGetValue(keyword, out var value))
        {
            return value;
        }

        return StaticAbstractValueResolver.ResolveOrUnknown(Keywords[keyword], bindings);
    }

    public bool TryResolveValue(int position, string keyword, AbstractState bindings, out AbstractValue value)
    {
        if (position < PositionalCount)
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
    internal static IReadOnlyDictionary<string, ExpressionSyntax> ReadKeywords(
        IReadOnlyList<CallArgumentSyntax> source, int positionalCount)
    {
        if (positionalCount == source.Count) return FrozenDictionary<string, ExpressionSyntax>.Empty;
        var keywords = new Dictionary<string, ExpressionSyntax>(source.Count - positionalCount, StringComparer.Ordinal);
        for (var i = positionalCount; i < source.Count; i++)
        {
            var argument = source[i];
            keywords[argument.KeywordName] = argument.Expression;
        }
        return keywords;
    }

    public static bool TryGetConcreteArguments(CallExpressionSyntax call, AbstractState bindings, out ConcreteCallArguments arguments)
    {
        if (call.TryGetConcreteArguments(out arguments)) return true;

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
                    keywords[argument.KeywordName] = argument.Expression;
                    keywordValues.Remove(argument.KeywordName);
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
                    positional.Add(item.Expression);
                    positionalValues.Add(null);
                }

                return true;

            case TupleLiteralExpressionSyntax tuple:
                foreach (var item in tuple.Items)
                {
                    positional.Add(item.Expression);
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

        foreach (var pair in value.RequireDictionaryItems())
        {
            if (pair.Key.Kind != AbstractValueKind.String)
            {
                return false;
            }

            var key = pair.Key.RequireText();
            keywords[key] = expression;
            keywordValues[key] = pair.Value;
        }

        return true;
    }
}
