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

            default:
                return false;
        }
    }

    private static void AnalyzeStringStartsEndsWithCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var parameterName = memberName == "startswith" ? "prefix" : "suffix";
        if (!arguments.TryGetValue(0, parameterName, out var prefixExpression) ||
            !arguments.TryResolveValue(0, parameterName, bindings, out var prefixValue))
        {
            return;
        }

        if (prefixValue.Kind == AbstractValueKind.String)
        {
            AnalyzeStringBoundsArguments(arguments, diagnostics, bindings);
            return;
        }

        if (prefixValue.Kind == AbstractValueKind.Tuple)
        {
            foreach (var item in prefixValue.RequireSequenceItems())
            {
                // A possible string match can skip every later tuple member.
                if (item.Kind is AbstractValueKind.String or AbstractValueKind.Unknown) break;
                AddDiagnostic(
                    diagnostics,
                    "LA3074",
                    memberName == "startswith"
                        ? $"tuple for startswith must only contain str, not {StaticAbstractFacts.DescribeValue(item)}"
                        : $"tuple for endswith must only contain str, not {StaticAbstractFacts.DescribeValue(item)}",
                    item.Span);
                return;
            }

            AnalyzeStringBoundsArguments(arguments, diagnostics, bindings);
            return;
        }

        if (prefixValue.IsLiteralLike)
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

        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 0, parameterName, "LA3075", message, diagnostics, bindings);

        AnalyzeStringBoundsArguments(arguments, diagnostics, bindings);
    }

    private static void AnalyzeStringSplitCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(
            arguments, 0, "sep", "LA3091",
            memberName == "split"
                ? "str.split([sep[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit."
                : "str.rsplit([sep[, maxsplit]]) expects zero, one, or two arguments with string separator and optional integer maxsplit.",
            diagnostics, bindings);

        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            1,
            "maxsplit",
            "LA3092",
            memberName == "split"
                ? "str.split([sep[, maxsplit]]) expects maxsplit to be an integer."
                : "str.rsplit([sep[, maxsplit]]) expects maxsplit to be an integer.",
            diagnostics,
            bindings,
            allowBoolean: true);
    }

    private static void AnalyzeStringStripCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(arguments, 0, "chars", "LA3093",
            $"str.{memberName}([chars]) expects zero or one string argument.", diagnostics, bindings);
    }

    private static void AnalyzeStringReplaceCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        const string message = "str.replace(old, new[, count]) expects two string arguments and an optional integer count.";
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 0, "old", "LA3094", message, diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownStringArgument(arguments, 1, "new", "LA3094", message, diagnostics, bindings);

        StaticContractChecks.AnalyzeOptionalIntegerArgument(arguments, 2, "count", "LA3095", "str.replace(old, new[, count]) expects count to be an integer.", diagnostics, bindings, allowBoolean: true);
    }

    private static void AnalyzeStringJoinCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "iterable", out var iterableExpression) ||
            !arguments.TryResolveValue(0, "iterable", bindings, out var iterable))
        {
            return;
        }

        if (iterable.Kind == AbstractValueKind.String)
        {
            return;
        }

        if (iterable.Kind is AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set)
        {
            var sequenceItems = iterable.RequireSequenceItems();
            if (sequenceItems.All(static item => item.IsLiteralLike))
            {
                StaticContractChecks.AnalyzeIterableOfStringsLiteral(sequenceItems, "LA3102", "str.join(iterable) expects an iterable of strings.", diagnostics);
            }
            return;
        }

        if (iterable.Kind is AbstractValueKind.Integer or AbstractValueKind.Float or AbstractValueKind.Boolean or AbstractValueKind.None)
        {
            AddDiagnostic(diagnostics, "LA3102", "str.join(iterable) expects an iterable of strings.", iterableExpression.Span);
        }
    }

    private static void AnalyzeStringBoundsArguments(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        const string message = "slice indices must be integers or None or have an __index__ method";
        StaticContractChecks.AnalyzeOptionalIntegerArgument(arguments, 1, "start", "LA3076", message, diagnostics, bindings, allowBoolean: true);
        StaticContractChecks.AnalyzeOptionalIntegerArgument(arguments, 2, "end", "LA3076", message, diagnostics, bindings, allowBoolean: true);
    }

}
