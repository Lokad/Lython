namespace Lokad.Lython.Frontend;

internal static class StaticContractChecks
{
    public static void AnalyzeKnownStringArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression))
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeKnownStringOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression) ||
            expression is NoneLiteralExpressionSyntax)
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeKnownStringKeywordArgument(
        ConcreteCallArguments arguments,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.Keywords.TryGetValue(keyword, out var expression))
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeKnownStringOrNoneKeywordArgument(
        ConcreteCallArguments arguments,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.Keywords.TryGetValue(keyword, out var expression) ||
            expression is NoneLiteralExpressionSyntax)
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out _) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeCallableOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression))
        {
            return;
        }

        if (expression is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownNonCallableLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeCallableOrNoneKeywordArgument(
        ConcreteCallArguments arguments,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.Keywords.TryGetValue(keyword, out var expression))
        {
            return;
        }

        if (expression is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownNonCallableLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeKnownBooleanArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression) ||
            expression is BooleanLiteralExpressionSyntax)
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeKnownBooleanKeywordArgument(
        ConcreteCallArguments arguments,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.Keywords.TryGetValue(keyword, out var expression) ||
            expression is BooleanLiteralExpressionSyntax)
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeKnownBooleanOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression) ||
            expression is NoneLiteralExpressionSyntax or BooleanLiteralExpressionSyntax)
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeOptionalIntegerArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression))
        {
            return;
        }

        if (expression is NoneLiteralExpressionSyntax ||
            StaticAbstractFacts.IsKnownIntegerLiteral(expression, bindings))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeTextBoundaryStringArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression))
        {
            return;
        }

        if (StaticAbstractValueResolver.IsDefinitelyKnownBytesLiteral(expression, bindings))
        {
            StaticDiagnosticSink.AddError(
                diagnostics,
                code,
                message,
                expression.Span,
                new StaticDiagnosticProof("contract", keyword, "bytes cannot cross a text-only host boundary"));
        }
    }

    public static void AnalyzeIterableOfStringsLiteral(IReadOnlyList<AbstractValue> items, string code, string message, List<LythonDiagnostic> diagnostics)
        => AnalyzeIterableOfStringsLiteral(items, code, message, diagnostics, false, null, null, null);

    public static void AnalyzeIterableOfStringsLiteral(IReadOnlyList<AbstractValue> items, string code, string message, List<LythonDiagnostic> diagnostics, bool requireNonEmpty)
        => AnalyzeIterableOfStringsLiteral(items, code, message, diagnostics, requireNonEmpty, null, null, null);

    public static void AnalyzeIterableOfStringsLiteral(IReadOnlyList<AbstractValue> items, string code, string message, List<LythonDiagnostic> diagnostics, bool requireNonEmpty, string? emptyCode)
        => AnalyzeIterableOfStringsLiteral(items, code, message, diagnostics, requireNonEmpty, emptyCode, null, null);

    public static void AnalyzeIterableOfStringsLiteral(IReadOnlyList<AbstractValue> items, string code, string message, List<LythonDiagnostic> diagnostics, bool requireNonEmpty, string? emptyCode, string? emptyMessage)
        => AnalyzeIterableOfStringsLiteral(items, code, message, diagnostics, requireNonEmpty, emptyCode, emptyMessage, null);

    public static void AnalyzeIterableOfStringsLiteral(
        IReadOnlyList<AbstractValue> items,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        bool requireNonEmpty,
        string? emptyCode,
        string? emptyMessage,
        LythonSourceSpan? emptySpan)
    {
        if (requireNonEmpty && items.Count == 0)
        {
            AddDiagnostic(diagnostics, emptyCode.RequireNotNull(), emptyMessage.RequireNotNull(), emptySpan.RequireNotNull());
            return;
        }

        if (StaticAbstractFacts.TryFindNonStringItem(items, out var span))
        {
            AddDiagnostic(diagnostics, code, message, span);
        }
    }

    public static void AnalyzeIterableOfPathLikeLiteral(IReadOnlyList<AbstractValue> items, string code, string message, List<LythonDiagnostic> diagnostics)
        => AnalyzeIterableOfPathLikeLiteral(items, code, message, diagnostics, false, null, null, null);

    public static void AnalyzeIterableOfPathLikeLiteral(IReadOnlyList<AbstractValue> items, string code, string message, List<LythonDiagnostic> diagnostics, bool requireNonEmpty)
        => AnalyzeIterableOfPathLikeLiteral(items, code, message, diagnostics, requireNonEmpty, null, null, null);

    public static void AnalyzeIterableOfPathLikeLiteral(IReadOnlyList<AbstractValue> items, string code, string message, List<LythonDiagnostic> diagnostics, bool requireNonEmpty, string? emptyCode)
        => AnalyzeIterableOfPathLikeLiteral(items, code, message, diagnostics, requireNonEmpty, emptyCode, null, null);

    public static void AnalyzeIterableOfPathLikeLiteral(IReadOnlyList<AbstractValue> items, string code, string message, List<LythonDiagnostic> diagnostics, bool requireNonEmpty, string? emptyCode, string? emptyMessage)
        => AnalyzeIterableOfPathLikeLiteral(items, code, message, diagnostics, requireNonEmpty, emptyCode, emptyMessage, null);

    public static void AnalyzeIterableOfPathLikeLiteral(
        IReadOnlyList<AbstractValue> items,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        bool requireNonEmpty,
        string? emptyCode,
        string? emptyMessage,
        LythonSourceSpan? emptySpan)
    {
        if (requireNonEmpty && items.Count == 0)
        {
            AddDiagnostic(diagnostics, emptyCode.RequireNotNull(), emptyMessage.RequireNotNull(), emptySpan.RequireNotNull());
            return;
        }

        foreach (var item in items)
        {
            if (!StaticKnownCallArgumentChecks.IsPathLike(item) &&
                !StaticKnownCallArgumentChecks.IsUnknown(item))
            {
                AddDiagnostic(diagnostics, code, message, item.Span);
                return;
            }
        }
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
