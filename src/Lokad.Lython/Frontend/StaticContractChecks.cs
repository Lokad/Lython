namespace Lokad.Lython.Frontend;

internal static class StaticContractChecks
{
    public static void AnalyzeKnownStringArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
        LythonDiagnosticCode code,
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
                expression.Span);
        }
    }

    public static bool AnalyzeSupportedTextErrorArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        LythonDiagnosticCode code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression) ||
            expression is NoneLiteralExpressionSyntax ||
            !StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out var text) ||
            StaticTextContractFacts.IsSupportedErrorName(text))
        {
            return false;
        }

        StaticDiagnosticSink.AddError(diagnostics, code, message, expression.Span);
        return true;
    }

    public static void AnalyzeIterableOfStringsLiteral(IReadOnlyList<AbstractValue> items, LythonDiagnosticCode code, string message, List<LythonDiagnostic> diagnostics)
    {
        if (StaticAbstractFacts.TryFindNonStringItem(items, out var span))
        {
            AddDiagnostic(diagnostics, code, message, span);
        }
    }

}
