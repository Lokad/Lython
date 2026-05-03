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
        => string.Equals(targetName, LythonKnownCallableSignatures.ArgparseArgumentParser.Name, StringComparison.Ordinal) &&
           AnalyzeStringArgument(arguments, 0, "description", "argparse.ArgumentParser([description]) expects description to be a string.", diagnostics, bindings);

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
                     actionText is not ("store" or "store_true" or "store_false" or "append" or "store_const"))
            {
                AddDiagnostic(diagnostics, "LA3051", "argparse.ArgumentParser.add_argument(..., action=...) only supports 'store', 'store_true', 'store_false', 'append', or 'store_const'.", actionExpression.Span);
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
                !StaticAbstractValueResolver.TryResolveKnownString(nargsExpression, bindings, out _))
            {
                if (StaticAbstractFacts.IsDefinitelyKnownLiteral(nargsExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3052", "argparse.ArgumentParser.add_argument(..., nargs=...) only supports positional nargs='*' or '+'.", nargsExpression.Span);
                }
            }
            else if (nargsExpression is not NoneLiteralExpressionSyntax &&
                     StaticAbstractValueResolver.TryResolveKnownString(nargsExpression, bindings, out var nargsText) &&
                     nargsText is not ("*" or "+"))
            {
                AddDiagnostic(diagnostics, "LA3052", "argparse.ArgumentParser.add_argument(..., nargs=...) only supports positional nargs='*' or '+'.", nargsExpression.Span);
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
}
