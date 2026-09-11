namespace Lokad.Lython.Frontend;

internal static class StaticCollectionContractFamily
{
    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is IdentifierExpressionSyntax { Name: "list" or "tuple" or "set" })
        {
            AnalyzeLiteralIterableBuiltinCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "len" })
        {
            AnalyzeLenCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "dict" })
        {
            AnalyzeDictCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "any" or "all" or "min" or "max" })
        {
            AnalyzeLiteralIterableBuiltinCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "sum" })
        {
            AnalyzeFirstIterableArgument(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is IdentifierExpressionSyntax { Name: "sorted" })
        {
            AnalyzeSortedCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: var listReceiver,
                MemberName: var listMember
            } &&
            StaticAbstractValueResolver.TryResolve(listReceiver, bindings, out var listValue) &&
            listValue.Kind is AbstractValueKind.List or AbstractValueKind.ListType)
        {
            AnalyzeListMemberCall(listMember, arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: var dictReceiver,
                MemberName: "update"
            } &&
            StaticAbstractValueResolver.TryResolve(dictReceiver, bindings, out var dictReceiverValue) &&
            dictReceiverValue.Kind == AbstractValueKind.Dict)
        {
            AnalyzeDictUpdateCall(arguments, diagnostics, bindings);
            return true;
        }

        return false;
    }

    private static void AnalyzeLiteralIterableBuiltinCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.Positional.Count + arguments.Keywords.Count != 1)
        {
            return;
        }

        if (!arguments.TryGetValue(0, "iterable", out var iterableExpression))
        {
            return;
        }

        AnalyzeIterableExpression(iterableExpression, diagnostics, bindings);
    }

    private static void AnalyzeFirstIterableArgument(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "iterable", out var iterableExpression))
        {
            return;
        }

        AnalyzeIterableExpression(iterableExpression, diagnostics, bindings);
    }

    private static void AnalyzeIterableExpression(ExpressionSyntax iterableExpression, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (StaticAbstractFacts.IsDefinitelyKnownNonIterable(iterableExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3031", "Object is not iterable.", iterableExpression.Span);
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownTextFileHandle(iterableExpression, bindings, out var mode) &&
            mode is AbstractTextFileMode.Write or AbstractTextFileMode.Append)
        {
            AddDiagnostic(diagnostics, "LA3109", "file is not open for reading.", iterableExpression.Span);
        }
    }

    private static void AnalyzeLenCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.Positional.Count + arguments.Keywords.Count != 1)
        {
            return;
        }

        if (!arguments.TryGetValue(0, "value", out var valueExpression))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownNonSized(valueExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3032", "Object has no len().", valueExpression.Span);
        }
    }

    private static void AnalyzeDictCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.Positional.Count + arguments.Keywords.Count != 1)
        {
            return;
        }

        if (!arguments.TryGetValue(0, "iterable", out var iterableExpression))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(iterableExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3031", "Object is not iterable.", iterableExpression.Span);
            return;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownSequenceItems(iterableExpression, bindings, out var items))
        {
            return;
        }

        foreach (var item in items)
        {
            if (StaticAbstractFacts.TryGetKnownSequenceArity(item, out var pairArity) && pairArity != 2)
            {
                AddDiagnostic(diagnostics, "LA3033", "dict(iterable_of_pairs) expects key-value pairs.", item.Span);
                return;
            }
        }
    }

    private static void AnalyzeSortedCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "iterable", out var iterableExpression) &&
            StaticAbstractFacts.IsDefinitelyKnownNonIterable(iterableExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3031", "Object is not iterable.", iterableExpression.Span);
        }

        if (arguments.TryGetValue(0, "iterable", out iterableExpression) &&
            StaticAbstractValueResolver.TryResolveKnownTextFileHandle(iterableExpression, bindings, out var mode) &&
            mode is AbstractTextFileMode.Write or AbstractTextFileMode.Append)
        {
            AddDiagnostic(diagnostics, "LA3109", "file is not open for reading.", iterableExpression.Span);
        }

        // R13: no static key-callable check here. An invalid key only fails when it
        // would actually be called, so empty input with a bad key succeeds.

    }

    private static void AnalyzeListMemberCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        switch (memberName)
        {
            case "extend":
                AnalyzeListExtendCall(arguments, diagnostics, bindings);
                break;
            case "index":
                AnalyzeOptionalIntegerArgument(arguments, 1, "start", "list.index(value[, start[, stop]]) expects integer start/stop bounds.", diagnostics, bindings);
                AnalyzeOptionalIntegerArgument(arguments, 2, "stop", "list.index(value[, start[, stop]]) expects integer start/stop bounds.", diagnostics, bindings);
                break;
            case "insert":
                AnalyzeOptionalIntegerArgument(arguments, 0, "index", "list.insert(index, value) expects an integer index.", diagnostics, bindings);
                break;
            case "pop":
                AnalyzeOptionalIntegerArgument(arguments, 0, "index", "list.pop([index]) expects an integer index.", diagnostics, bindings);
                break;
            case "sort":
                AnalyzeListSortCall(arguments, diagnostics, bindings);
                break;
        }
    }

    private static void AnalyzeListExtendCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "iterable", out var iterableExpression))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownNonIterable(iterableExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3140", "list.extend(iterable) expects an iterable.", iterableExpression.Span);
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownTextFileHandle(iterableExpression, bindings, out var mode) &&
            mode is AbstractTextFileMode.Write or AbstractTextFileMode.Append)
        {
            AddDiagnostic(diagnostics, "LA3109", "file is not open for reading.", iterableExpression.Span);
        }
    }

    private static void AnalyzeListSortCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.Positional.Count > 0)
        {
            AddDiagnostic(diagnostics, "LA3123", "list.sort(*, key=None, reverse=False) expects keyword-only arguments.", arguments.Positional[0].Span);
        }

        // R13: no static key-callable check here; an invalid key only fails when
        // the list is non-empty and the key would actually be called.

    }

    private static void AnalyzeOptionalIntegerArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression))
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolve(expression, bindings, out var value) &&
            value.Kind != AbstractValueKind.Unknown &&
            !StaticAbstractFacts.IsIntegerLike(value))
        {
            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return;
        }
    }

    private static void AnalyzeDictUpdateCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "mapping", out var mappingExpression))
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolve(mappingExpression, bindings, out var value))
        {
            if (value.Kind == AbstractValueKind.List || value.Kind == AbstractValueKind.Tuple || value.Kind == AbstractValueKind.Set)
            {
                // Pair sequences validate their elements at runtime like CPython.
                return;
            }

            if (value.Kind != AbstractValueKind.Unknown && value.Kind != AbstractValueKind.Dict)
            {
                AddDiagnostic(diagnostics, "LA3104", "dict.update(mapping) expects one dictionary argument.", mappingExpression.Span);
                return;
            }
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(mappingExpression, bindings) &&
            (!StaticAbstractValueResolver.TryResolveKnownValue(mappingExpression, bindings, out var knownValue) ||
             (knownValue.Kind != AbstractValueKind.Dict && knownValue.Kind != AbstractValueKind.Set)))
        {
            AddDiagnostic(diagnostics, "LA3104", "dict.update(mapping) expects one dictionary argument.", mappingExpression.Span);
        }
    }

}
