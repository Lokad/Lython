using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticProcessContractFamily
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!string.Equals(targetName, LythonKnownCallableSignatures.SubprocessRun.Name, StringComparison.Ordinal))
        {
            return false;
        }

        var emitted = AnalyzeIterableOfStringsArgument(arguments, 0, "args", "subprocess.run(args) expects a non-empty iterable of strings, not a single string.", diagnostics, bindings, rejectSingleString: true, requireNonEmpty: true);
        emitted |= AnalyzeStringOrNoneArgument(arguments, 1, "input", "subprocess.run(..., input=...) expects a string or None.", diagnostics, bindings);
        emitted |= AnalyzeStringOrNoneArgument(arguments, 2, "cwd", "subprocess.run(..., cwd=...) expects a string or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, 3, "timeout", "subprocess.run(..., timeout=...) expects an integer or None.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, 4, "check", "subprocess.run(..., check=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, 5, "capture_output", "subprocess.run(..., capture_output=...) expects a bool or None.", diagnostics, bindings);
        return emitted;
    }

    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is not MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "subprocess" },
                MemberName: "run"
            })
        {
            return false;
        }

        AnalyzeSubprocessRunCall(arguments, diagnostics, bindings);
        return true;
    }

    private static void AnalyzeSubprocessRunCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "args", out var argsExpression))
        {
            if (StaticAbstractValueResolver.TryResolveKnownString(argsExpression, bindings, out _))
            {
                AddDiagnostic(diagnostics, "LA3020", "subprocess.run(args) expects an iterable of strings, not a single string.", argsExpression.Span);
            }
            else if (StaticAbstractValueResolver.TryResolveKnownSequenceItems(argsExpression, bindings, out var sequenceItems))
            {
                StaticContractChecks.AnalyzeIterableOfStringsLiteral(
                    sequenceItems,
                    "LA3021",
                    "subprocess.run(args) expects an iterable of strings.",
                    diagnostics,
                    requireNonEmpty: true,
                    emptyCode: "LA3027",
                    emptyMessage: "subprocess.run(args) expects at least one command part.",
                    emptySpan: argsExpression.Span);
            }
            else if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(argsExpression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3021", "subprocess.run(args) expects an iterable of strings.", argsExpression.Span);
            }
        }

        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(
            arguments,
            1,
            "input",
            "LA3022",
            "subprocess.run(..., input=...) expects a string or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(
            arguments,
            2,
            "cwd",
            "LA3023",
            "subprocess.run(..., cwd=...) expects a string or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            3,
            "timeout",
            "LA3024",
            "subprocess.run(..., timeout=...) expects an integer or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            4,
            "check",
            "LA3025",
            "subprocess.run(..., check=...) expects a bool or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            5,
            "capture_output",
            "LA3026",
            "subprocess.run(..., capture_output=...) expects a bool or None.",
            diagnostics,
            bindings);
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
