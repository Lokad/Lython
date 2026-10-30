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

        if (arguments.TryResolveValue(position, keyword, bindings, out var value) &&
            value.IsLiteralLike && value.Kind != AbstractValueKind.String)
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
        if (!arguments.TryGetValue(position, keyword, out var expression))
        {
            return;
        }

        if (arguments.TryResolveValue(position, keyword, bindings, out var value) &&
            value.IsLiteralLike && value.Kind is not AbstractValueKind.String and not AbstractValueKind.None)
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

        var value = arguments.ResolveKeywordValue(keyword, bindings);
        if (value.IsLiteralLike && value.Kind != AbstractValueKind.String)
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
        if (!arguments.Keywords.TryGetValue(keyword, out var expression))
        {
            return;
        }

        var value = arguments.ResolveKeywordValue(keyword, bindings);
        if (value.IsLiteralLike && value.Kind is not AbstractValueKind.String and not AbstractValueKind.None)
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

    public static void AnalyzeOptionalIntegerArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        LythonDiagnosticCode code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        bool allowBoolean)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression) ||
            !arguments.TryResolveValue(position, keyword, bindings, out var value))
        {
            return;
        }

        if (value.Kind is AbstractValueKind.None or AbstractValueKind.Integer ||
            (allowBoolean && value.Kind == AbstractValueKind.Boolean))
        {
            return;
        }

        if (value.IsLiteralLike)
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
