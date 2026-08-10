using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticArgparseContractFamily
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.ArgparseArgumentParser.Name, StringComparison.Ordinal))
        {
            var emitted = false;
            emitted |= AnalyzeStringOrNoneArgument(arguments, 0, "prog", "argparse.ArgumentParser(..., prog=...) expects a string or None.", diagnostics, bindings);
            emitted |= AnalyzeStringOrNoneArgument(arguments, 1, "usage", "argparse.ArgumentParser(..., usage=...) expects a string or None.", diagnostics, bindings);
            emitted |= AnalyzeStringOrNoneArgument(arguments, 2, "description", "argparse.ArgumentParser(..., description=...) expects a string or None.", diagnostics, bindings);
            emitted |= AnalyzeStringOrNoneArgument(arguments, 3, "epilog", "argparse.ArgumentParser(..., epilog=...) expects a string or None.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 5, "add_help", "argparse.ArgumentParser(..., add_help=...) expects a bool.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 6, "allow_abbrev", "argparse.ArgumentParser(..., allow_abbrev=...) expects a bool.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 7, "exit_on_error", "argparse.ArgumentParser(..., exit_on_error=...) expects a bool.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.ArgparseFileType.Name, StringComparison.Ordinal))
        {
            var emitted = false;
            emitted |= AnalyzeStringArgument(arguments, 0, "mode", "argparse.FileType(..., mode=...) expects a string.", diagnostics, bindings);
            emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "bufsize", "argparse.FileType(..., bufsize=...) expects an integer or None.", diagnostics, bindings);
            emitted |= AnalyzeStringOrNoneArgument(arguments, 2, "encoding", "argparse.FileType(..., encoding=...) expects a string or None.", diagnostics, bindings);
            emitted |= AnalyzeStringOrNoneArgument(arguments, 3, "errors", "argparse.FileType(..., errors=...) expects a string or None.", diagnostics, bindings);
            emitted |= AnalyzeTextEncoding(arguments, 2, "encoding", "argparse.FileType only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.", diagnostics, bindings);
            emitted |= AnalyzeTextErrors(arguments, 3, "errors", "argparse.FileType only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is not MemberExpressionSyntax { MemberName: var memberName })
        {
            return false;
        }

        switch (memberName)
        {
            case "add_argument":
                AnalyzeArgparseAddArgumentCall(arguments, diagnostics, bindings);
                return true;
            case "add_mutually_exclusive_group":
                AnalyzeArgparseMutuallyExclusiveGroupCall(arguments, diagnostics, bindings);
                return true;
            case "parse_args":
                AnalyzeArgparseParseArgsCall(arguments, diagnostics, bindings);
                return true;
            case "parse_known_args":
                AnalyzeArgparseParseArgsCall(arguments, diagnostics, bindings);
                return true;
            default:
                return false;
        }
    }

    private static void AnalyzeArgparseAddArgumentCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.Positional.Count == 0)
        {
            return;
        }

        for (var i = 0; i < arguments.Positional.Count; i++)
        {
            var optionName = arguments.Positional[i];
            if (!StaticAbstractValueResolver.TryResolveKnownString(optionName, bindings, out var optionText))
            {
                if (StaticAbstractFacts.IsDefinitelyKnownLiteral(optionName, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3055", "argparse.ArgumentParser.add_argument(...) expects option names to be strings.", optionName.Span);
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(optionText))
            {
                AddDiagnostic(diagnostics, "LA3056", "argparse.ArgumentParser.add_argument(...) expects a valid option name.", optionName.Span);
            }
        }

        StaticContractChecks.AnalyzeKnownStringKeywordArgument(
            arguments,
            "dest",
            "LA3057",
            "argparse.ArgumentParser.add_argument(..., dest=...) expects a string.",
            diagnostics,
            bindings);

        if (arguments.Keywords.TryGetValue("action", out var actionExpression))
        {
            if (actionExpression is not NoneLiteralExpressionSyntax &&
                !StaticAbstractValueResolver.TryResolveKnownString(actionExpression, bindings, out _))
            {
                if (StaticAbstractFacts.IsDefinitelyKnownLiteral(actionExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3058", "argparse.ArgumentParser.add_argument(..., action=...) expects a string.", actionExpression.Span);
                }
            }
            else if (actionExpression is not NoneLiteralExpressionSyntax &&
                     StaticAbstractValueResolver.TryResolveKnownString(actionExpression, bindings, out var actionText) &&
                     actionText is not ("store" or "store_true" or "store_false" or "append" or "store_const" or "count" or "version"))
            {
                AddDiagnostic(diagnostics, "LA3051", "argparse.ArgumentParser.add_argument(..., action=...) only supports 'store', 'store_true', 'store_false', 'append', 'store_const', 'count', or 'version'.", actionExpression.Span);
            }
        }

        StaticContractChecks.AnalyzeCallableOrNoneKeywordArgument(
            arguments,
            "type",
            "LA3050",
            "argparse.ArgumentParser.add_argument(..., type=...) expects a callable or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownBooleanKeywordArgument(
            arguments,
            "required",
            "LA3105",
            "argparse.ArgumentParser.add_argument(..., required=...) expects a bool.",
            diagnostics,
            bindings);

        if (arguments.Keywords.TryGetValue("nargs", out var nargsExpression))
        {
            if (nargsExpression is not NoneLiteralExpressionSyntax &&
                !StaticAbstractValueResolver.TryResolveKnownString(nargsExpression, bindings, out _) &&
                !IsKnownPositiveInteger(nargsExpression, bindings))
            {
                if (StaticAbstractFacts.IsDefinitelyKnownLiteral(nargsExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3052", "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.", nargsExpression.Span);
                }
            }
            else if (nargsExpression is not NoneLiteralExpressionSyntax &&
                     StaticAbstractValueResolver.TryResolveKnownString(nargsExpression, bindings, out var nargsText) &&
                     !IsSupportedNargsText(nargsText))
            {
                AddDiagnostic(diagnostics, "LA3052", "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.", nargsExpression.Span);
            }
        }

        StaticContractChecks.AnalyzeKnownStringOrNoneKeywordArgument(
            arguments,
            "help",
            "LA3059",
            "argparse.ArgumentParser.add_argument(..., help=...) expects a string.",
            diagnostics,
            bindings);

        if (arguments.Keywords.TryGetValue("choices", out var choicesExpression) &&
            StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(choicesExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3031", "Object is not iterable.", choicesExpression.Span);
        }
    }

    private static void AnalyzeArgparseParseArgsCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "args", out var argsExpression))
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownString(argsExpression, bindings, out _))
        {
            AddDiagnostic(diagnostics, "LA3064", "argparse.ArgumentParser.parse_args(args) expects an iterable of strings, not a single string.", argsExpression.Span);
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownSequenceItems(argsExpression, bindings, out var sequenceItems))
        {
            StaticContractChecks.AnalyzeIterableOfStringsLiteral(sequenceItems, "LA3065", "argparse.ArgumentParser.parse_args(args) expects an iterable of strings.", diagnostics);
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(argsExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3065", "argparse.ArgumentParser.parse_args(args) expects an iterable of strings.", argsExpression.Span);
        }
    }

    private static void AnalyzeArgparseMutuallyExclusiveGroupCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        StaticContractChecks.AnalyzeKnownBooleanArgument(
            arguments,
            0,
            "required",
            "LA3097",
            "argparse.ArgumentParser.add_mutually_exclusive_group([required]) expects required to be a bool.",
            diagnostics,
            bindings);
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }

    private static bool IsSupportedNargsText(string text)
        => text is "?" or "*" or "+" ||
           (int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var count) && count > 0);

    private static bool IsKnownPositiveInteger(ExpressionSyntax expression, AbstractState bindings)
    {
        if (!StaticAbstractValueResolver.TryResolve(expression, bindings, out var value) ||
            !StaticAbstractFacts.IsIntegerLike(value))
        {
            return false;
        }

        return value.Kind != AbstractValueKind.Integer ||
               !int.TryParse(value.RequirePayload<string>(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var literal) ||
               literal > 0;
    }

    private static bool AnalyzeTextEncoding(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression) ||
            expression is NoneLiteralExpressionSyntax ||
            !StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out var text) ||
            text.Equals("utf-8", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("utf-8-sig", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("latin-1", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("latin1", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        StaticDiagnosticSink.AddError(diagnostics, "LA3151", message, expression.Span);
        return true;
    }

    private static bool AnalyzeTextErrors(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
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

        StaticDiagnosticSink.AddError(diagnostics, "LA3151", message, expression.Span);
        return true;
    }

}
