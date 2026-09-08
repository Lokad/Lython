namespace Lokad.Lython.Frontend;

internal static class StaticStringContractFamily
{
    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is not MemberExpressionSyntax { Target: var receiver, MemberName: var memberName } ||
            !StaticAbstractValueResolver.TryResolveKnownStringLike(receiver, bindings))
        {
            return false;
        }

        switch (memberName)
        {
            case "startswith":
            case "endswith":
                AnalyzeStringStartsEndsWithCall(memberName, arguments, diagnostics, bindings);
                return true;

            case "find":
            case "index":
            case "rfind":
            case "rindex":
            case "count":
            case "removeprefix":
            case "removesuffix":
            case "partition":
            case "rpartition":
                AnalyzeStringSingleStringArgumentCall(memberName, arguments, diagnostics, bindings);
                return true;

            case "split":
            case "rsplit":
                AnalyzeStringSplitCall(memberName, arguments, diagnostics, bindings);
                return true;

            case "strip":
            case "lstrip":
            case "rstrip":
                AnalyzeStringStripCall(memberName, arguments, diagnostics, bindings);
                return true;

            case "replace":
                AnalyzeStringReplaceCall(arguments, diagnostics, bindings);
                return true;

            case "join":
                AnalyzeStringJoinCall(arguments, diagnostics, bindings);
                return true;

            case "format_map":
                AnalyzeStringFormatMapCall(arguments, diagnostics, bindings);
                return true;

            default:
                return false;
        }
    }

    private static void AnalyzeStringStartsEndsWithCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var parameterName = memberName == "startswith" ? "prefix" : "suffix";
        if (!arguments.TryGetValue(0, parameterName, out var prefixExpression))
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownString(prefixExpression, bindings, out _))
        {
            AnalyzeStringBoundsArguments(arguments, diagnostics, bindings);
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownValue(prefixExpression, bindings, out var prefixValue) &&
            prefixValue.Kind == AbstractValueKind.Tuple)
        {
            foreach (var item in prefixValue.RequireSequenceItems())
            {
                if (item.Kind != AbstractValueKind.String)
                {
                    AddDiagnostic(
                        diagnostics,
                        "LA3074",
                        memberName == "startswith"
                            ? $"tuple for startswith must only contain str, not {StaticAbstractFacts.DescribeValue(item)}"
                            : $"tuple for endswith must only contain str, not {StaticAbstractFacts.DescribeValue(item)}",
                        item.Span);
                    return;
                }
            }

            AnalyzeStringBoundsArguments(arguments, diagnostics, bindings);
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(prefixExpression, bindings))
        {
            AddDiagnostic(
                diagnostics,
                "LA3073",
                memberName == "startswith"
                    ? "str.startswith(prefix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds."
                    : "str.endswith(suffix[, start[, end]]) expects a string or tuple of strings, plus optional integer bounds.",
                prefixExpression.Span);
        }

        AnalyzeStringBoundsArguments(arguments, diagnostics, bindings);
    }

    private static void AnalyzeStringSingleStringArgumentCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        string parameterName;
        string message;

        switch (memberName)
        {
            case "find":
                parameterName = "sub";
                message = "str.find(sub[, start[, end]]) expects one string argument plus optional integer bounds.";
                break;
            case "index":
                parameterName = "sub";
                message = "str.index(sub[, start[, end]]) expects one string argument plus optional integer bounds.";
                break;
            case "rfind":
                parameterName = "sub";
                message = "str.rfind(sub[, start[, end]]) expects one string argument plus optional integer bounds.";
                break;
            case "rindex":
                parameterName = "sub";
                message = "str.rindex(sub[, start[, end]]) expects one string argument plus optional integer bounds.";
                break;
            case "count":
                parameterName = "sub";
                message = "str.count(sub[, start[, end]]) expects one string argument plus optional integer bounds.";
                break;
            case "removeprefix":
                parameterName = "prefix";
                message = "str.removeprefix(prefix) expects one string argument.";
                break;
            case "removesuffix":
                parameterName = "suffix";
                message = "str.removesuffix(suffix) expects one string argument.";
                break;
            case "partition":
                parameterName = "sep";
                message = "str.partition(sep) expects one string argument.";
                break;
            case "rpartition":
                parameterName = "sep";
                message = "str.rpartition(sep) expects one string argument.";
                break;
            default:
                return;
        }

        if (!arguments.TryGetValue(0, parameterName, out var expression))
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3075", message, expression.Span);
        }

        AnalyzeStringBoundsArguments(arguments, diagnostics, bindings);
    }

    private static void AnalyzeStringSplitCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "separator", out var separatorExpression) &&
            separatorExpression is not NoneLiteralExpressionSyntax &&
            !StaticAbstractValueResolver.TryResolveKnownString(separatorExpression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(separatorExpression, bindings))
        {
            AddDiagnostic(
                diagnostics,
                "LA3091",
                memberName == "split"
                    ? "str.split([separator[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit."
                    : "str.rsplit([separator[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.",
                separatorExpression.Span);
        }

        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            1,
            "maxsplit",
            "LA3092",
            memberName == "split"
                ? "str.split([separator[, maxsplit]]) expects maxsplit to be an integer."
                : "str.rsplit([separator[, maxsplit]]) expects maxsplit to be an integer.",
            diagnostics,
            bindings,
            allowBoolean: true);
    }

    private static void AnalyzeStringStripCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "chars", out var charsExpression))
        {
            return;
        }

        if (charsExpression is not NoneLiteralExpressionSyntax &&
            !StaticAbstractValueResolver.TryResolveKnownString(charsExpression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(charsExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3093", $"str.{memberName}([chars]) expects zero or one string argument.", charsExpression.Span);
        }
    }

    private static void AnalyzeStringReplaceCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "old", out var oldExpression) &&
            !StaticAbstractValueResolver.TryResolveKnownString(oldExpression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(oldExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3094", "str.replace(old, new[, count]) expects two string arguments and an optional integer count.", oldExpression.Span);
        }

        if (arguments.TryGetValue(1, "new", out var newExpression) &&
            !StaticAbstractValueResolver.TryResolveKnownString(newExpression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(newExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3094", "str.replace(old, new[, count]) expects two string arguments and an optional integer count.", newExpression.Span);
        }

        StaticContractChecks.AnalyzeOptionalIntegerArgument(arguments, 2, "count", "LA3095", "str.replace(old, new[, count]) expects count to be an integer.", diagnostics, bindings, allowBoolean: true);
    }

    private static void AnalyzeStringJoinCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "iterable", out var iterableExpression))
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownString(iterableExpression, bindings, out _))
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownSequenceItems(iterableExpression, bindings, out var sequenceItems))
        {
            StaticContractChecks.AnalyzeIterableOfStringsLiteral(sequenceItems, "LA3102", "str.join(iterable) expects an iterable of strings.", diagnostics);
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(iterableExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3102", "str.join(iterable) expects an iterable of strings.", iterableExpression.Span);
        }
    }

    private static void AnalyzeStringFormatMapCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "mapping", out var mappingExpression))
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownValue(mappingExpression, bindings, out var value) &&
            value.Kind != AbstractValueKind.Dict)
        {
            AddDiagnostic(diagnostics, "LA3103", "str.format_map(mapping) expects one dictionary argument.", mappingExpression.Span);
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(mappingExpression, bindings) &&
            (!StaticAbstractValueResolver.TryResolveKnownValue(mappingExpression, bindings, out var knownValue) ||
             knownValue.Kind != AbstractValueKind.Dict))
        {
            AddDiagnostic(diagnostics, "LA3103", "str.format_map(mapping) expects one dictionary argument.", mappingExpression.Span);
        }
    }

    private static void AnalyzeStringBoundsArguments(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        AnalyzeStringBoundArgument(arguments, 1, "start", diagnostics, bindings);
        AnalyzeStringBoundArgument(arguments, 2, "end", diagnostics, bindings);
    }

    private static void AnalyzeStringBoundArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression))
        {
            return;
        }

        if (expression is NoneLiteralExpressionSyntax ||
            StaticAbstractFacts.IsKnownIntegerLiteral(expression, bindings) ||
            StaticAbstractFacts.IsKnownBooleanLiteral(expression, bindings))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3076", "slice indices must be integers or None or have an __index__ method", expression.Span);
        }
    }

}
