using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticDataModuleContractFamily
{
    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonLoads.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "s", "json.loads(s) expects a string argument.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvReader.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeIterableOfStringsArgument(arguments, 0, "lines", "csv.reader(lines[, delimiter]) expects an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "delimiter", "csv.reader(lines, delimiter) expects delimiter to be a string.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvWriter.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "delimiter", "csv.writer([delimiter]) expects delimiter to be a string.", diagnostics, bindings);
        }

        return false;
    }

    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "csv" },
                MemberName: "reader"
            })
        {
            AnalyzeCsvReaderCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "fnmatch" },
                MemberName: "filter"
            })
        {
            AnalyzeFnmatchFilterCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "random" },
                MemberName: "choices"
            })
        {
            AnalyzeRandomChoicesCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "functools" },
                MemberName: "update_wrapper"
            })
        {
            AnalyzeFunctoolsUpdateWrapperCall(arguments, diagnostics, bindings);
            return true;
        }

        return false;
    }

    private static void AnalyzeCsvReaderCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "lines", out var linesExpression))
        {
            if (StaticAbstractValueResolver.TryResolveKnownSequenceItems(linesExpression, bindings, out var sequenceItems))
            {
                StaticContractChecks.AnalyzeIterableOfStringsLiteral(sequenceItems, "LA3066", "csv.reader(lines) expects an iterable of strings.", diagnostics);
            }
            else if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(linesExpression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3066", "csv.reader(lines) expects an iterable of strings.", linesExpression.Span);
            }
        }

        if (arguments.TryGetValue(1, "delimiter", out var delimiterExpression))
        {
            if (!StaticAbstractValueResolver.TryResolveKnownString(delimiterExpression, bindings, out var delimiterText))
            {
                if (StaticAbstractFacts.IsDefinitelyKnownLiteral(delimiterExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3067", "csv delimiter must be a string.", delimiterExpression.Span);
                }

                return;
            }

            if (delimiterText.Length != 1)
            {
                AddDiagnostic(diagnostics, "LA3068", "csv delimiter must be one character.", delimiterExpression.Span);
            }
            else if (delimiterText[0] is '\r' or '\n')
            {
                AddDiagnostic(diagnostics, "LA3069", "csv delimiter cannot be a newline.", delimiterExpression.Span);
            }
        }
    }

    private static void AnalyzeFnmatchFilterCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "names", out var namesExpression))
        {
            if (StaticAbstractValueResolver.TryResolveKnownSequenceItems(namesExpression, bindings, out var sequenceItems))
            {
                StaticContractChecks.AnalyzeIterableOfStringsLiteral(sequenceItems, "LA3070", "fnmatch.filter(names, pattern) expects an iterable of strings.", diagnostics);
            }
            else if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(namesExpression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3070", "fnmatch.filter(names, pattern) expects an iterable of strings.", namesExpression.Span);
            }
        }

        StaticContractChecks.AnalyzeKnownStringArgument(
            arguments,
            1,
            "pattern",
            "LA3071",
            "fnmatch.filter(names, pattern) expects an iterable and a string pattern.",
            diagnostics,
            bindings);
    }

    private static void AnalyzeRandomChoicesCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(1, "weights", out var weightsExpression) &&
            weightsExpression is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(weightsExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3098", "random.choices(..., weights=...) expects an iterable of numeric weights.", weightsExpression.Span);
        }

        if (arguments.TryGetValue(2, "cum_weights", out var cumulativeExpression) &&
            cumulativeExpression is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(cumulativeExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3099", "random.choices(..., cum_weights=...) expects an iterable of numeric weights.", cumulativeExpression.Span);
        }

        if (arguments.TryGetValue(1, "weights", out var weightsArgument) &&
            arguments.TryGetValue(2, "cum_weights", out var cumulativeArgument) &&
            weightsArgument is not NoneLiteralExpressionSyntax &&
            cumulativeArgument is not NoneLiteralExpressionSyntax &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(weightsArgument, bindings) &&
            StaticAbstractFacts.IsDefinitelyKnownLiteral(cumulativeArgument, bindings))
        {
            AddDiagnostic(diagnostics, "LA3100", "random.choices(...) does not accept both weights and cum_weights.", cumulativeArgument.Span);
        }

        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            3,
            "k",
            "LA3101",
            "random.choices(..., k=...) expects k to be a non-negative integer.",
            diagnostics,
            bindings);
    }

    private static void AnalyzeFunctoolsUpdateWrapperCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(0, "wrapper", out var wrapperExpression))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(wrapperExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3106", "functools.update_wrapper(wrapper, wrapped) expects a mutable callable wrapper and a wrapped object.", wrapperExpression.Span);
        }
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
