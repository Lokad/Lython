using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static partial class StaticBindingEngine
{
    private static bool TryApplyArgparseParserMutation(ExpressionSyntax expression, AbstractState bindings)
    {
        if (expression is not CallExpressionSyntax
            {
                Target: MemberExpressionSyntax
                {
                    Target: IdentifierExpressionSyntax { Name: var receiverName },
                    MemberName: "add_argument"
                }
            } call ||
            !TryResolveArgparseParserMutationTarget(receiverName, bindings, out var parserName, out var parserValue) ||
            !StaticCallArguments.TryGetConcreteArguments(call, bindings, out var arguments) ||
            !TryInferArgparseDestination(arguments, bindings, out var destination))
        {
            return false;
        }

        var parser = (AbstractArgparseParserSummary)parserValue.Value;
        var members = new Dictionary<string, AbstractValue>(parser.Members, StringComparer.Ordinal)
        {
            [destination] = InferArgparseMemberValue(arguments, bindings, call.Span)
        };

        bindings.Set(parserName, AbstractValue.ArgparseParser(new AbstractArgparseParserSummary(members, parser.IsSealed), call.Span));
        return true;
    }

    private static bool TryResolveArgparseParserMutationTarget(
        string receiverName,
        AbstractState bindings,
        out string parserName,
        out AbstractValue parserValue)
    {
        if (bindings.TryGet(receiverName, out parserValue) &&
            parserValue.Kind == AbstractValueKind.ArgparseParser)
        {
            parserName = receiverName;
            return true;
        }

        if (bindings.TryGet(receiverName, out var groupValue) &&
            groupValue.Kind == AbstractValueKind.ArgparseMutuallyExclusiveGroup)
        {
            var group = (AbstractArgparseGroupSummary)groupValue.Value;
            if (!string.IsNullOrEmpty(group.ParserName) &&
                bindings.TryGet(group.ParserName, out parserValue) &&
                parserValue.Kind == AbstractValueKind.ArgparseParser)
            {
                parserName = group.ParserName;
                return true;
            }
        }

        parserName = string.Empty;
        parserValue = default;
        return false;
    }

    private static bool TryInferArgparseDestination(ConcreteCallArguments arguments, AbstractState bindings, out string destination)
    {
        if (arguments.Keywords.TryGetValue("dest", out var destExpression) &&
            StaticAbstractValueResolver.TryResolveKnownString(destExpression, bindings, out var explicitDestination) &&
            !string.IsNullOrWhiteSpace(explicitDestination))
        {
            destination = explicitDestination;
            return true;
        }

        var optionNames = new List<string>(arguments.Positional.Count);
        foreach (var optionExpression in arguments.Positional)
        {
            if (StaticAbstractValueResolver.TryResolveKnownString(optionExpression, bindings, out var optionName))
            {
                optionNames.Add(optionName);
                continue;
            }

            destination = string.Empty;
            return false;
        }

        for (var i = optionNames.Count - 1; i >= 0; i--)
        {
            var optionName = optionNames[i];
            if (optionName.StartsWith("--", StringComparison.Ordinal) && optionName.Length > 2)
            {
                destination = NormalizeArgparseDestination(optionName);
                return true;
            }
        }

        for (var i = optionNames.Count - 1; i >= 0; i--)
        {
            var optionName = optionNames[i];
            if (optionName.StartsWith("-", StringComparison.Ordinal) && optionName.Length > 1)
            {
                destination = NormalizeArgparseDestination(optionName);
                return true;
            }
        }

        foreach (var optionName in optionNames)
        {
            if (!optionName.StartsWith("-", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(optionName))
            {
                destination = optionName;
                return true;
            }
        }

        destination = string.Empty;
        return false;
    }

    private static string NormalizeArgparseDestination(string optionName)
        => optionName.TrimStart('-').Replace('-', '_');

    private static AbstractValue InferArgparseMemberValue(
        ConcreteCallArguments arguments,
        AbstractState bindings,
        LythonSourceSpan span)
    {
        var action = TryGetKnownStringKeyword(arguments, bindings, "action", out var actionText)
            ? actionText
            : "store";

        if (action == "store_true" || action == "store_false")
        {
            return AbstractValue.BooleanType(span);
        }

        var isOptional = IsArgparseOptional(arguments, bindings);
        var required = TryGetKnownBooleanKeyword(arguments, "required", out var requiredValue) && requiredValue;
        var hasDefault = TryResolveArgparseDefault(arguments, bindings, out var defaultValue);
        var scalarValue = ResolveArgparseScalarValue(arguments, span);

        if (action == "append")
        {
            var listValue = AbstractValue.ListOf(scalarValue, span);
            if (hasDefault)
            {
                return AbstractValue.Join(listValue, defaultValue, span);
            }

            return isOptional && !required ? AbstractValue.Unknown(span) : listValue;
        }

        if (TryGetKnownStringKeyword(arguments, bindings, "nargs", out var nargs) && nargs is "*" or "+")
        {
            var listValue = AbstractValue.ListOf(scalarValue, span);
            if (hasDefault)
            {
                return AbstractValue.Join(listValue, defaultValue, span);
            }

            return isOptional && !required && nargs == "*" ? AbstractValue.Unknown(span) : listValue;
        }

        if (action == "store_const")
        {
            if (arguments.Keywords.TryGetValue("const", out var constExpression))
            {
                var constValue = StaticAbstractValueResolver.ResolveOrUnknown(constExpression, bindings).WithSpan(span);
                return hasDefault ? AbstractValue.Join(constValue, defaultValue, span) : constValue;
            }

            return hasDefault ? defaultValue.WithSpan(span) : AbstractValue.Unknown(span);
        }

        if (hasDefault)
        {
            return AbstractValue.Join(scalarValue, defaultValue, span);
        }

        return isOptional && !required ? AbstractValue.Unknown(span) : scalarValue;
    }

    private static AbstractValue ResolveArgparseScalarValue(ConcreteCallArguments arguments, LythonSourceSpan span)
    {
        if (!arguments.Keywords.TryGetValue("type", out var typeExpression))
        {
            return AbstractValue.StringType(span);
        }

        while (typeExpression is ParenthesizedExpressionSyntax parenthesized)
        {
            typeExpression = parenthesized.Inner;
        }

        return typeExpression switch
        {
            IdentifierExpressionSyntax { Name: "int" } => AbstractValue.IntegerType(span),
            IdentifierExpressionSyntax { Name: "float" } => AbstractValue.FloatType(span),
            IdentifierExpressionSyntax { Name: "str" } => AbstractValue.StringType(span),
            IdentifierExpressionSyntax { Name: "bool" } => AbstractValue.BooleanType(span),
            _ => AbstractValue.Unknown(span)
        };
    }

    private static bool TryResolveArgparseDefault(ConcreteCallArguments arguments, AbstractState bindings, out AbstractValue value)
    {
        if (arguments.Keywords.TryGetValue("default", out var defaultExpression) &&
            defaultExpression is not NoneLiteralExpressionSyntax)
        {
            value = StaticAbstractValueResolver.ResolveOrUnknown(defaultExpression, bindings);
            return true;
        }

        value = default;
        return false;
    }

    private static bool IsArgparseOptional(ConcreteCallArguments arguments, AbstractState bindings)
    {
        foreach (var optionExpression in arguments.Positional)
        {
            if (StaticAbstractValueResolver.TryResolveKnownString(optionExpression, bindings, out var optionName) &&
                optionName.StartsWith("-", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetKnownStringKeyword(ConcreteCallArguments arguments, AbstractState bindings, string keyword, [MaybeNullWhen(false)] out string value)
    {
        if (arguments.Keywords.TryGetValue(keyword, out var expression))
        {
            return StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out value);
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetKnownBooleanKeyword(ConcreteCallArguments arguments, string keyword, out bool value)
    {
        if (arguments.Keywords.TryGetValue(keyword, out var expression))
        {
            while (expression is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized.Inner;
            }

            if (expression is BooleanLiteralExpressionSyntax boolean)
            {
                value = boolean.Value;
                return true;
            }
        }

        value = false;
        return false;
    }

}
