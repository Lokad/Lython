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
            var emitted = AnalyzeCsvInputArgument(arguments, 0, "csvfile", "csv.reader(csvfile) expects an iterable of strings or a readable text file handle.", diagnostics, bindings);
            AnalyzeCsvOptions(arguments, dialectPosition: 1, delimiterPosition: 2, quotecharPosition: 3, quotingPosition: 4, doublequotePosition: 5, escapecharPosition: 6, skipinitialspacePosition: 7, lineterminatorPosition: 8, strictPosition: 9, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvWriter.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeOptionalCsvWriterFileArgument(arguments, 0, "fileobj", diagnostics, bindings);
            AnalyzeCsvOptions(arguments, dialectPosition: 1, delimiterPosition: 2, quotecharPosition: 3, quotingPosition: 4, doublequotePosition: 5, escapecharPosition: 6, skipinitialspacePosition: 7, lineterminatorPosition: 8, strictPosition: 9, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvDictReader.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeCsvInputArgument(arguments, 0, "f", "csv.DictReader(f) expects an iterable of strings or a readable text file handle.", diagnostics, bindings);
            emitted |= AnalyzeIterableOfStringsArgument(arguments, 1, "fieldnames", "csv.DictReader(..., fieldnames=...) expects an iterable of strings.", diagnostics, bindings);
            AnalyzeCsvOptions(arguments, dialectPosition: 4, delimiterPosition: 5, quotecharPosition: 6, quotingPosition: 7, doublequotePosition: 8, escapecharPosition: 9, skipinitialspacePosition: 10, lineterminatorPosition: 11, strictPosition: 12, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CsvDictWriter.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeRequiredCsvWriterFileArgument(arguments, 0, "f", diagnostics, bindings);
            emitted |= AnalyzeIterableOfStringsArgument(arguments, 1, "fieldnames", "csv.DictWriter(..., fieldnames=...) expects an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzeDictWriterExtrasAction(arguments, diagnostics, bindings);
            AnalyzeCsvOptions(arguments, dialectPosition: 4, delimiterPosition: 5, quotecharPosition: 6, quotingPosition: 7, doublequotePosition: 8, escapecharPosition: 9, skipinitialspacePosition: 10, lineterminatorPosition: 11, strictPosition: 12, diagnostics, bindings);
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
                Target: IdentifierExpressionSyntax { Name: "csv" },
                MemberName: "DictReader"
            })
        {
            AnalyzeCsvDictReaderCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "csv" },
                MemberName: "DictWriter"
            })
        {
            AnalyzeCsvDictWriterCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: var dictWriterReceiver,
                MemberName: "writerow"
            } &&
            StaticAbstractValueResolver.TryResolve(dictWriterReceiver, bindings, out var dictWriterValue) &&
            dictWriterValue.Kind == AbstractValueKind.CsvDictWriter)
        {
            AnalyzeDictWriterRowCall(arguments, diagnostics, bindings);
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
        AnalyzeCsvInputArgument(arguments, 0, "csvfile", "csv.reader(csvfile) expects an iterable of strings or a readable text file handle.", diagnostics, bindings);
        AnalyzeCsvOptions(arguments, dialectPosition: 1, delimiterPosition: 2, quotecharPosition: 3, quotingPosition: 4, doublequotePosition: 5, escapecharPosition: 6, skipinitialspacePosition: 7, lineterminatorPosition: 8, strictPosition: 9, diagnostics, bindings);
    }

    private static void AnalyzeCsvDictReaderCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        AnalyzeCsvInputArgument(arguments, 0, "f", "csv.DictReader(f) expects an iterable of strings or a readable text file handle.", diagnostics, bindings);
        AnalyzeIterableOfStringsArgument(arguments, 1, "fieldnames", "csv.DictReader(..., fieldnames=...) expects an iterable of strings.", diagnostics, bindings);
        AnalyzeCsvOptions(arguments, dialectPosition: 4, delimiterPosition: 5, quotecharPosition: 6, quotingPosition: 7, doublequotePosition: 8, escapecharPosition: 9, skipinitialspacePosition: 10, lineterminatorPosition: 11, strictPosition: 12, diagnostics, bindings);
    }

    private static void AnalyzeCsvDictWriterCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        AnalyzeRequiredCsvWriterFileArgument(arguments, 0, "f", diagnostics, bindings);
        AnalyzeIterableOfStringsArgument(arguments, 1, "fieldnames", "csv.DictWriter(..., fieldnames=...) expects an iterable of strings.", diagnostics, bindings);
        AnalyzeDictWriterExtrasAction(arguments, diagnostics, bindings);
        AnalyzeCsvOptions(arguments, dialectPosition: 4, delimiterPosition: 5, quotecharPosition: 6, quotingPosition: 7, doublequotePosition: 8, escapecharPosition: 9, skipinitialspacePosition: 10, lineterminatorPosition: 11, strictPosition: 12, diagnostics, bindings);
    }

    private static bool AnalyzeCsvInputArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var linesExpression))
        {
            return false;
        }

        if (StaticAbstractValueResolver.TryResolveKnownTextFileHandle(linesExpression, bindings, out var mode))
        {
            if (mode is AbstractTextFileMode.Write or AbstractTextFileMode.Append)
            {
                AddDiagnostic(diagnostics, "LA3109", "file is not open for reading.", linesExpression.Span);
                return true;
            }

            return false;
        }

        if (arguments.TryGetValue(position, keyword, out linesExpression))
        {
            if (StaticAbstractValueResolver.TryResolveKnownSequenceItems(linesExpression, bindings, out var sequenceItems))
            {
                StaticContractChecks.AnalyzeIterableOfStringsLiteral(sequenceItems, "LA3066", message, diagnostics);
            }
            else if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(linesExpression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3066", message, linesExpression.Span);
            }
        }

        return false;
    }

    private static bool AnalyzeOptionalCsvWriterFileArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.None)
        {
            return false;
        }

        if (value.IsStringLike)
        {
            return false;
        }

        return AnalyzeCsvWriterFileValue(expression, value, allowMissing: true, diagnostics);
    }

    private static bool AnalyzeRequiredCsvWriterFileArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        return AnalyzeCsvWriterFileValue(expression, value, allowMissing: false, diagnostics);
    }

    private static bool AnalyzeCsvWriterFileValue(ExpressionSyntax expression, AbstractValue value, bool allowMissing, List<LythonDiagnostic> diagnostics)
    {
        if (value.Kind == AbstractValueKind.Unknown)
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.TextFileHandle)
        {
            var mode = (AbstractTextFileMode)value.Value;
            if (mode == AbstractTextFileMode.Read)
            {
                AddDiagnostic(diagnostics, "LA3111", "file is not open for writing.", expression.Span);
                return true;
            }

            return false;
        }

        if (allowMissing && value.Kind == AbstractValueKind.None)
        {
            return false;
        }

        AddDiagnostic(diagnostics, "LA3067", "csv writer expects a writable text file handle.", expression.Span);
        return true;
    }

    private static void AnalyzeCsvOptions(
        ConcreteCallArguments arguments,
        int dialectPosition,
        int delimiterPosition,
        int quotecharPosition,
        int quotingPosition,
        int doublequotePosition,
        int escapecharPosition,
        int skipinitialspacePosition,
        int lineterminatorPosition,
        int strictPosition,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        AnalyzeCsvDialect(arguments, dialectPosition, diagnostics, bindings);
        AnalyzeCsvCharacterOption(arguments, delimiterPosition, "delimiter", allowNone: false, diagnostics, bindings);
        AnalyzeCsvCharacterOption(arguments, quotecharPosition, "quotechar", allowNone: true, diagnostics, bindings);
        AnalyzeCsvQuoting(arguments, quotingPosition, diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, doublequotePosition, "doublequote", "csv doublequote must be a bool.", diagnostics, bindings);
        AnalyzeCsvCharacterOption(arguments, escapecharPosition, "escapechar", allowNone: true, diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, skipinitialspacePosition, "skipinitialspace", "csv skipinitialspace must be a bool.", diagnostics, bindings);
        AnalyzeStringArgument(arguments, lineterminatorPosition, "lineterminator", "csv lineterminator must be a string.", diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, strictPosition, "strict", "csv strict must be a bool.", diagnostics, bindings);
    }

    private static void AnalyzeCsvDialect(ConcreteCallArguments arguments, int position, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, "dialect", out var dialectExpression))
        {
            return;
        }

        if (dialectExpression is NoneLiteralExpressionSyntax)
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownString(dialectExpression, bindings, out var text) &&
            (string.Equals(text, "excel", StringComparison.Ordinal) || text.Length == 1))
        {
            return;
        }

        if (StaticAbstractFacts.IsDefinitelyKnownLiteral(dialectExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3067", "csv dialect registry is unsupported; pass explicit CSV options instead.", dialectExpression.Span);
        }
    }

    private static void AnalyzeCsvCharacterOption(ConcreteCallArguments arguments, int position, string keyword, bool allowNone, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression))
        {
            return;
        }

        if (allowNone && expression is NoneLiteralExpressionSyntax)
        {
            return;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out var text))
        {
            if (StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3067", $"csv {keyword} must be a string.", expression.Span);
            }

            return;
        }

        if (text.Length != 1)
        {
            AddDiagnostic(diagnostics, "LA3068", $"csv {keyword} must be one character.", expression.Span);
        }
        else if (keyword == "delimiter" && text[0] is '\r' or '\n')
        {
            AddDiagnostic(diagnostics, "LA3069", "csv delimiter cannot be a newline.", expression.Span);
        }
    }

    private static void AnalyzeCsvQuoting(ConcreteCallArguments arguments, int position, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, "quoting", bindings, out var expression, out var value))
        {
            return;
        }

        if (value.Kind == AbstractValueKind.Unknown || value.Kind == AbstractValueKind.None)
        {
            return;
        }

        if (value.Kind != AbstractValueKind.Integer && value.Kind != AbstractValueKind.IntegerType)
        {
            AddDiagnostic(diagnostics, "LA3067", "csv quoting must be one of the QUOTE_* constants.", expression.Span);
            return;
        }

        if (StaticKnownCallArgumentChecks.TryGetInt32(value, out var integer) && (integer < 0 || integer > 3))
        {
            AddDiagnostic(diagnostics, "LA3067", "csv quoting must be one of the QUOTE_* constants.", expression.Span);
        }
    }

    private static bool AnalyzeDictWriterExtrasAction(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!arguments.TryGetValue(3, "extrasaction", out var expression))
        {
            return false;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownString(expression, bindings, out var text))
        {
            if (StaticAbstractFacts.IsDefinitelyKnownLiteral(expression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3067", "csv.DictWriter(..., extrasaction=...) expects a string.", expression.Span);
                return true;
            }

            return false;
        }

        if (!string.Equals(text, "raise", StringComparison.Ordinal) &&
            !string.Equals(text, "ignore", StringComparison.Ordinal))
        {
            AddDiagnostic(diagnostics, "LA3067", "csv.DictWriter(..., extrasaction=...) expects 'raise' or 'ignore'.", expression.Span);
            return true;
        }

        return false;
    }

    private static void AnalyzeDictWriterRowCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 0, "rowdict", bindings, out var expression, out var value))
        {
            return;
        }

        if (value.Kind is AbstractValueKind.Unknown or AbstractValueKind.Dict)
        {
            return;
        }

        AddDiagnostic(diagnostics, "LA3067", "csv.DictWriter.writerow(rowdict) expects a dictionary.", expression.Span);
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
