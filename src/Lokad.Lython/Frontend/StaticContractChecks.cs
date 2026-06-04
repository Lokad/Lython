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

    public static void AnalyzeKnownNonByteStringArgument(
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
            !StaticAbstractValueResolver.IsDefinitelyKnownBytesLiteral(expression, bindings) &&
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
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    public static void AnalyzeIterableOfStringsLiteral(
        IReadOnlyList<AbstractValue> items,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        bool requireNonEmpty = false,
        string? emptyCode = null,
        string? emptyMessage = null,
        LythonSourceSpan? emptySpan = null)
    {
        if (requireNonEmpty && items.Count == 0)
        {
            AddDiagnostic(diagnostics, emptyCode!, emptyMessage!, emptySpan!);
            return;
        }

        if (StaticAbstractFacts.TryFindNonStringItem(items, out var span))
        {
            AddDiagnostic(diagnostics, code, message, span);
        }
    }

    public static void AnalyzeIterableOfPathLikeLiteral(
        IReadOnlyList<AbstractValue> items,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        bool requireNonEmpty = false,
        string? emptyCode = null,
        string? emptyMessage = null,
        LythonSourceSpan? emptySpan = null)
    {
        if (requireNonEmpty && items.Count == 0)
        {
            AddDiagnostic(diagnostics, emptyCode!, emptyMessage!, emptySpan!);
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
