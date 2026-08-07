using System.Globalization;

namespace Lokad.Lython.Frontend;

internal static class StaticKnownCallArgumentChecks
{
    internal static bool AnalyzeStringArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.IsStringLike);

    internal static bool AnalyzePathLikeArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, IsPathLike);

    internal static bool AnalyzePathLikeOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || IsPathLike(value));

    internal static bool AnalyzeStringOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.IsStringLike || value.Kind == AbstractValueKind.None);

    internal static bool AnalyzeIntegerArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, IsRuntimeIntegerLike);

    internal static bool AnalyzeIntegerOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || IsRuntimeIntegerLike(value));

    internal static bool AnalyzeBooleanArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, IsBooleanLike);

    internal static bool AnalyzeBooleanOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || IsBooleanLike(value));

    internal static bool AnalyzeCallableOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || !StaticAbstractFacts.IsDefinitelyNonCallable(value));

    internal static bool AnalyzeStringOrCallableArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.IsStringLike || !StaticAbstractFacts.IsDefinitelyNonCallable(value));

    internal static bool AnalyzeRegexPatternArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.IsStringLike || value.Kind == AbstractValueKind.RegexPattern);

    internal static bool AnalyzeIterableOfStringsArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        bool rejectSingleString = false,
        bool requireNonEmpty = false)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (rejectSingleString && value.IsStringLike)
        {
            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        if (value.Kind is AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set)
        {
            var items = (IReadOnlyList<AbstractValue>)value.Value;
            if (requireNonEmpty && items.Count == 0)
            {
                AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
                return true;
            }

            foreach (var item in items)
            {
                if (!item.IsStringLike && !IsUnknown(item))
                {
                    AddDiagnostic(diagnostics, "LA3158", message, DiagnosticSpan(expression, item));
                    return true;
                }
            }

            return false;
        }

        if (value.Kind is AbstractValueKind.ListType or AbstractValueKind.SetType)
        {
            var item = (AbstractValue)value.Value;
            if (!item.IsStringLike && !IsUnknown(item))
            {
                AddDiagnostic(diagnostics, "LA3158", message, DiagnosticSpan(expression, item));
                return true;
            }

            return false;
        }

        if (StaticAbstractFacts.IsDefinitelyNonIterable(value))
        {
            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        return false;
    }

    internal static bool AnalyzeIterableOfPathLikeArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        bool rejectSinglePathLike = false,
        bool requireNonEmpty = false)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (rejectSinglePathLike && IsPathLike(value))
        {
            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        if (value.Kind is AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set)
        {
            var items = (IReadOnlyList<AbstractValue>)value.Value;
            if (requireNonEmpty && items.Count == 0)
            {
                AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
                return true;
            }

            foreach (var item in items)
            {
                if (!IsPathLike(item) && !IsUnknown(item))
                {
                    AddDiagnostic(diagnostics, "LA3158", message, DiagnosticSpan(expression, item));
                    return true;
                }
            }

            return false;
        }

        if (value.Kind is AbstractValueKind.ListType or AbstractValueKind.SetType)
        {
            var item = (AbstractValue)value.Value;
            if (!IsPathLike(item) && !IsUnknown(item))
            {
                AddDiagnostic(diagnostics, "LA3158", message, DiagnosticSpan(expression, item));
                return true;
            }

            return false;
        }

        if (StaticAbstractFacts.IsDefinitelyNonIterable(value))
        {
            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        return false;
    }

    internal static bool AnalyzeArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        Func<AbstractValue, bool> accepts)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        return AnalyzeKnownArgumentValue(expression, value, message, diagnostics, accepts);
    }

    internal static bool AnalyzeKnownArgumentValue(
        ExpressionSyntax expression,
        AbstractValue value,
        string message,
        List<LythonDiagnostic> diagnostics,
        Func<AbstractValue, bool> accepts)
    {
        if (IsUnknown(value) || accepts(value))
        {
            return false;
        }

        AddDiagnostic(diagnostics, "LA3158", message, DiagnosticSpan(expression, value));
        return true;
    }

    internal static bool TryGetArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        AbstractState bindings,
        [MaybeNullWhen(false)] out ExpressionSyntax expression,
        out AbstractValue value)
    {
        if (!arguments.TryGetValue(position, keyword, out expression))
        {
            value = default;
            return false;
        }

        value = arguments.TryResolveValue(position, keyword, bindings, out var resolved)
            ? resolved
            : StaticAbstractValueResolver.ResolveOrUnknown(expression, bindings);
        return true;
    }

    internal static bool IsUnknown(AbstractValue value)
        => value.Kind is AbstractValueKind.Unknown or AbstractValueKind.Never;

    internal static bool IsRuntimeIntegerLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Integer or AbstractValueKind.IntegerType;

    internal static bool IsBooleanLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Boolean or AbstractValueKind.BooleanType;

    internal static bool IsPathLike(AbstractValue value)
        => value.IsStringLike || value.Kind == AbstractValueKind.Path;

    internal static LythonSourceSpan DiagnosticSpan(ExpressionSyntax expression, AbstractValue value)
        => value.Span.Length != 0 ? value.Span : expression.Span;

    internal static bool TryGetInt32(AbstractValue value, out int integer)
    {
        if (value.Kind == AbstractValueKind.Integer)
        {
            return int.TryParse(
                ((string)value.Value).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out integer);
        }

        integer = 0;
        return false;
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
