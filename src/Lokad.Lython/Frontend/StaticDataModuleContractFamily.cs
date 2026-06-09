using System.Globalization;
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

        if (AnalyzeDifflibKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings))
        {
            return true;
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

        if (call.Target is MemberExpressionSyntax { Target: var receiverExpression, MemberName: var memberName } &&
            StaticAbstractValueResolver.TryResolve(receiverExpression, bindings, out var receiver))
        {
            if (receiver.Kind == AbstractValueKind.DifflibDiffer &&
                memberName == "compare")
            {
                AnalyzeDifflibLineIterable(arguments, 0, "a", "Differ.compare(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
                AnalyzeDifflibLineIterable(arguments, 1, "b", "Differ.compare(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.DifflibHtmlDiff &&
                memberName is "make_table" or "make_file")
            {
                AnalyzeHtmlDiffCall(memberName, arguments, diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.DifflibSequenceMatcher)
            {
                AnalyzeSequenceMatcherMemberCall(memberName, arguments, diagnostics, bindings);
                return true;
            }
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

    private static bool AnalyzeDifflibKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibIsLineJunk.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "line", "difflib.IS_LINE_JUNK(line) expects a string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibIsCharacterJunk.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "ch", "difflib.IS_CHARACTER_JUNK(ch) expects a string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibUnifiedDiff.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.DifflibContextDiff.Name, StringComparison.Ordinal))
        {
            var owner = targetName.EndsWith("unified_diff", StringComparison.Ordinal) ? "difflib.unified_diff" : "difflib.context_diff";
            var emitted = AnalyzeDifflibLineIterable(arguments, 0, "a", $"{owner}(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
            emitted |= AnalyzeDifflibLineIterable(arguments, 1, "b", $"{owner}(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 2, "fromfile", $"{owner}(..., fromfile=...) expects a string or None.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 3, "tofile", $"{owner}(..., tofile=...) expects a string or None.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 4, "fromfiledate", $"{owner}(..., fromfiledate=...) expects a string or None.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 5, "tofiledate", $"{owner}(..., tofiledate=...) expects a string or None.", diagnostics, bindings);
            AnalyzeIntegerArgument(arguments, 6, "n", $"{owner}(..., n=...) expects an integer.", diagnostics, bindings);
            AnalyzeStringOrNoneArgument(arguments, 7, "lineterm", $"{owner}(..., lineterm=...) expects a string or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibNdiff.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeDifflibLineIterable(arguments, 0, "a", "difflib.ndiff(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
            emitted |= AnalyzeDifflibLineIterable(arguments, 1, "b", "difflib.ndiff(a, b) expects a and b to be iterables of strings.", diagnostics, bindings);
            AnalyzeCallableOrNoneArgument(arguments, 2, "linejunk", "difflib.ndiff(..., linejunk=...) expects a callable or None.", diagnostics, bindings);
            AnalyzeCallableOrNoneArgument(arguments, 3, "charjunk", "difflib.ndiff(..., charjunk=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibRestore.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeDifflibLineIterable(arguments, 0, "delta", "difflib.restore(delta, which) expects delta to be an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzeRestoreWhich(arguments, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibGetCloseMatches.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeStringArgument(arguments, 0, "word", "difflib.get_close_matches(word, possibilities) expects word to be a string.", diagnostics, bindings);
            emitted |= AnalyzeDifflibLineIterable(arguments, 1, "possibilities", "difflib.get_close_matches(word, possibilities) expects possibilities to be an iterable of strings.", diagnostics, bindings);
            emitted |= AnalyzePositiveIntegerArgument(arguments, 2, "n", "difflib.get_close_matches(..., n=...) expects n to be positive.", diagnostics, bindings);
            emitted |= AnalyzeCutoffArgument(arguments, diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibDiffBytes.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeArgument(arguments, 0, "dfunc", "difflib.diff_bytes(dfunc, ...) expects dfunc to be callable.", diagnostics, bindings, static value => !StaticAbstractFacts.IsDefinitelyNonCallable(value));
            emitted |= AnalyzeIterableOfBytesArgument(arguments, 1, "a", "difflib.diff_bytes(..., a=...) expects an iterable of bytes.", diagnostics, bindings);
            emitted |= AnalyzeIterableOfBytesArgument(arguments, 2, "b", "difflib.diff_bytes(..., b=...) expects an iterable of bytes.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 3, "fromfile", "difflib.diff_bytes(..., fromfile=...) expects bytes or None.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 4, "tofile", "difflib.diff_bytes(..., tofile=...) expects bytes or None.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 5, "fromfiledate", "difflib.diff_bytes(..., fromfiledate=...) expects bytes or None.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 6, "tofiledate", "difflib.diff_bytes(..., tofiledate=...) expects bytes or None.", diagnostics, bindings);
            emitted |= AnalyzeIntegerArgument(arguments, 7, "n", "difflib.diff_bytes(..., n=...) expects an integer.", diagnostics, bindings);
            emitted |= AnalyzeBytesOrNoneArgument(arguments, 8, "lineterm", "difflib.diff_bytes(..., lineterm=...) expects bytes or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibDiffer.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeCallableOrNoneArgument(arguments, 0, "linejunk", "difflib.Differ(..., linejunk=...) expects a callable or None.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 1, "charjunk", "difflib.Differ(..., charjunk=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibHtmlDiff.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeIntegerArgument(arguments, 0, "tabsize", "difflib.HtmlDiff(..., tabsize=...) expects an integer.", diagnostics, bindings);
            emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "wrapcolumn", "difflib.HtmlDiff(..., wrapcolumn=...) expects an integer or None.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 2, "linejunk", "difflib.HtmlDiff(..., linejunk=...) expects a callable or None.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 3, "charjunk", "difflib.HtmlDiff(..., charjunk=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.DifflibSequenceMatcher.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeCallableOrNoneArgument(arguments, 0, "isjunk", "difflib.SequenceMatcher(..., isjunk=...) expects a callable or None.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 3, "autojunk", "difflib.SequenceMatcher(..., autojunk=...) expects a bool.", diagnostics, bindings);
            return emitted;
        }

        return false;
    }

    private static bool AnalyzeDifflibLineIterable(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeIterableOfStringsArgument(arguments, position, keyword, message, diagnostics, bindings);

    private static bool AnalyzeIterableOfBytesArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (value.Kind is AbstractValueKind.Bytes or AbstractValueKind.BytesType)
        {
            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        if (value.Kind is AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set)
        {
            foreach (var item in (IReadOnlyList<AbstractValue>)value.Value)
            {
                if (!IsBytesLike(item) && !IsUnknown(item))
                {
                    AddDiagnostic(diagnostics, "LA3158", message, item.Span);
                    return true;
                }
            }

            return false;
        }

        if (value.Kind == AbstractValueKind.ListType)
        {
            var item = (AbstractValue)value.Value;
            if (!IsBytesLike(item) && !IsUnknown(item))
            {
                AddDiagnostic(diagnostics, "LA3158", message, item.Span);
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

    private static bool AnalyzeBytesOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || IsBytesLike(value));

    private static bool AnalyzeRestoreWhich(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 1, "which", bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (TryGetInt32(value, out var which) && which is 1 or 2)
        {
            return false;
        }

        if (value.Kind is AbstractValueKind.Integer or AbstractValueKind.IntegerType)
        {
            AddDiagnostic(diagnostics, "LA3158", "difflib.restore(delta, which) expects which to be 1 or 2.", expression.Span);
            return true;
        }

        AddDiagnostic(diagnostics, "LA3158", "difflib.restore(delta, which) expects which to be an integer 1 or 2.", expression.Span);
        return true;
    }

    private static bool AnalyzeCutoffArgument(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 3, "cutoff", bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (!TryGetDouble(value, out var cutoff))
        {
            if (StaticAbstractFacts.IsNumericLike(value))
            {
                return false;
            }

            AddDiagnostic(diagnostics, "LA3158", "difflib.get_close_matches(..., cutoff=...) expects a number between 0 and 1.", expression.Span);
            return true;
        }

        if (cutoff < 0.0 || cutoff > 1.0)
        {
            AddDiagnostic(diagnostics, "LA3158", "difflib.get_close_matches(..., cutoff=...) expects cutoff between 0 and 1.", expression.Span);
            return true;
        }

        return false;
    }

    private static bool AnalyzePositiveIntegerArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (IsUnknown(value))
        {
            return false;
        }

        if (TryGetInt32(value, out var integer))
        {
            if (integer > 0)
            {
                return false;
            }

            AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
            return true;
        }

        if (value.Kind is AbstractValueKind.IntegerType)
        {
            return false;
        }

        AddDiagnostic(diagnostics, "LA3158", message, expression.Span);
        return true;
    }

    private static void AnalyzeHtmlDiffCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        AnalyzeDifflibLineIterable(arguments, 0, "fromlines", $"HtmlDiff.{memberName}(fromlines, tolines) expects iterables of strings.", diagnostics, bindings);
        AnalyzeDifflibLineIterable(arguments, 1, "tolines", $"HtmlDiff.{memberName}(fromlines, tolines) expects iterables of strings.", diagnostics, bindings);
        AnalyzeStringOrNoneArgument(arguments, 2, "fromdesc", $"HtmlDiff.{memberName}(..., fromdesc=...) expects a string or None.", diagnostics, bindings);
        AnalyzeStringOrNoneArgument(arguments, 3, "todesc", $"HtmlDiff.{memberName}(..., todesc=...) expects a string or None.", diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, 4, "context", $"HtmlDiff.{memberName}(..., context=...) expects a bool.", diagnostics, bindings);
        AnalyzeIntegerArgument(arguments, 5, "numlines", $"HtmlDiff.{memberName}(..., numlines=...) expects an integer.", diagnostics, bindings);
        if (memberName == "make_file")
        {
            AnalyzeStringOrNoneArgument(arguments, 6, "charset", "HtmlDiff.make_file(..., charset=...) expects a string or None.", diagnostics, bindings);
        }
    }

    private static void AnalyzeSequenceMatcherMemberCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (memberName)
        {
            case "find_longest_match":
                AnalyzeIntegerOrNoneArgument(arguments, 0, "alo", "SequenceMatcher.find_longest_match(..., alo=...) expects an integer or None.", diagnostics, bindings);
                AnalyzeIntegerOrNoneArgument(arguments, 1, "ahi", "SequenceMatcher.find_longest_match(..., ahi=...) expects an integer or None.", diagnostics, bindings);
                AnalyzeIntegerOrNoneArgument(arguments, 2, "blo", "SequenceMatcher.find_longest_match(..., blo=...) expects an integer or None.", diagnostics, bindings);
                AnalyzeIntegerOrNoneArgument(arguments, 3, "bhi", "SequenceMatcher.find_longest_match(..., bhi=...) expects an integer or None.", diagnostics, bindings);
                break;
            case "get_grouped_opcodes":
                AnalyzeIntegerArgument(arguments, 0, "n", "SequenceMatcher.get_grouped_opcodes([n]) expects an integer.", diagnostics, bindings);
                break;
        }
    }

    private static bool IsBytesLike(AbstractValue value)
        => value.Kind is AbstractValueKind.Bytes or AbstractValueKind.BytesType;

    private static bool TryGetDouble(AbstractValue value, out double number)
    {
        if (value.Kind == AbstractValueKind.Integer &&
            int.TryParse(
                ((string)value.Value).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var integer))
        {
            number = integer;
            return true;
        }

        if (value.Kind == AbstractValueKind.Float &&
            double.TryParse(
                ((string)value.Value).Replace("_", string.Empty, StringComparison.Ordinal),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var floating))
        {
            number = floating;
            return true;
        }

        if (value.Kind == AbstractValueKind.Boolean)
        {
            number = (bool)value.Value ? 1.0 : 0.0;
            return true;
        }

        if (value.Kind is AbstractValueKind.IntegerType or AbstractValueKind.FloatType)
        {
            number = 0.0;
            return false;
        }

        number = 0.0;
        return false;
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
