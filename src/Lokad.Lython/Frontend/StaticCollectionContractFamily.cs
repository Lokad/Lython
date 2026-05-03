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

        if (call.Target is IdentifierExpressionSyntax { Name: "sorted" })
        {
            AnalyzeSortedCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: var listReceiver,
                MemberName: "extend"
            } &&
            StaticAbstractValueResolver.TryResolve(listReceiver, bindings, out var listValue) &&
            listValue.Kind is AbstractValueKind.List or AbstractValueKind.ListType)
        {
            AnalyzeListExtendCall(arguments, diagnostics, bindings);
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

        if (arguments.TryGetValue(1, "key", out var keyExpression) &&
            keyExpression is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownNonCallableLiteral(keyExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3034", "sorted(..., key=...) expects a callable or None.", keyExpression.Span);
        }

        if (arguments.TryGetValue(2, "reverse", out var reverseExpression) &&
            reverseExpression is not BooleanLiteralExpressionSyntax)
        {
            if (StaticAbstractFacts.IsDefinitelyKnownLiteral(reverseExpression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3035", "sorted(..., reverse=...) expects a bool.", reverseExpression.Span);
            }
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

    private static void AnalyzeDictUpdateCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "mapping", out var mappingExpression))
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolve(mappingExpression, bindings, out var value) &&
            value.Kind != AbstractValueKind.Unknown &&
            value.Kind != AbstractValueKind.Dict)
        {
            AddDiagnostic(diagnostics, "LA3104", "dict.update(mapping) expects one dictionary argument.", mappingExpression.Span);
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(mappingExpression, bindings) &&
            (!StaticAbstractValueResolver.TryResolveKnownValue(mappingExpression, bindings, out var knownValue) ||
             knownValue.Kind != AbstractValueKind.Dict))
        {
            AddDiagnostic(diagnostics, "LA3104", "dict.update(mapping) expects one dictionary argument.", mappingExpression.Span);
        }
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
