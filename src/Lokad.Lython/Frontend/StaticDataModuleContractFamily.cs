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
        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonLoad.Name, StringComparison.Ordinal))
        {
            AnalyzeJsonReadableFileArgument(arguments, 0, "fp", diagnostics, bindings);
            AnalyzeJsonLoadOptions(arguments, diagnostics, bindings, optionOffset: 1);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonLoads.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeStringArgument(arguments, 0, "s", "json.loads(s, *, ...) expects a string argument.", diagnostics, bindings);
            AnalyzeJsonLoadOptions(arguments, diagnostics, bindings, optionOffset: 1);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonDump.Name, StringComparison.Ordinal))
        {
            AnalyzeJsonWritableFileArgument(arguments, 1, "fp", diagnostics, bindings);
            AnalyzeJsonDumpOptions(arguments, diagnostics, bindings, optionOffset: 2);
            return true;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.JsonDumps.Name, StringComparison.Ordinal))
        {
            AnalyzeJsonDumpOptions(arguments, diagnostics, bindings, optionOffset: 1);
            return false;
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

        if (AnalyzePkgutilKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (AnalyzeCopyKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings))
        {
            return true;
        }

        if (AnalyzeRandomKnownCallArgumentTypes(targetName, arguments, diagnostics, bindings))
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
                Target: IdentifierExpressionSyntax { Name: "functools" },
                MemberName: "update_wrapper"
            })
        {
            AnalyzeFunctoolsUpdateWrapperCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is MemberExpressionSyntax
            {
                Target: IdentifierExpressionSyntax { Name: "random" },
                MemberName: "SystemRandom"
            })
        {
            AddDiagnostic(diagnostics, "LA3158", "random.SystemRandom(...) is unsupported by Lython because system entropy is not exposed.", call.Span);
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

            if (receiver.Kind == AbstractValueKind.PkgutilModuleInfo)
            {
                AnalyzePkgutilModuleInfoMemberCall(memberName, arguments, diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.PkgutilLoader)
            {
                AnalyzePkgutilLoaderMemberCall(memberName, arguments, diagnostics, bindings);
                return true;
            }

            if (receiver.Kind == AbstractValueKind.Random)
            {
                AnalyzeRandomMemberCall(memberName, arguments, diagnostics, bindings);
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

    private static void AnalyzeJsonLoadOptions(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        int optionOffset)
    {
        AnalyzeJsonClsNone(arguments, optionOffset, "cls", diagnostics, bindings);
        AnalyzeCallableOrNoneArgument(arguments, optionOffset + 1, "object_hook", "json load option object_hook=... expects a callable or None.", diagnostics, bindings);
        AnalyzeCallableOrNoneArgument(arguments, optionOffset + 2, "parse_float", "json load option parse_float=... expects a callable or None.", diagnostics, bindings);
        AnalyzeCallableOrNoneArgument(arguments, optionOffset + 3, "parse_int", "json load option parse_int=... expects a callable or None.", diagnostics, bindings);
        AnalyzeCallableOrNoneArgument(arguments, optionOffset + 4, "parse_constant", "json load option parse_constant=... expects a callable or None.", diagnostics, bindings);
        AnalyzeCallableOrNoneArgument(arguments, optionOffset + 5, "object_pairs_hook", "json load option object_pairs_hook=... expects a callable or None.", diagnostics, bindings);
    }

    private static void AnalyzeJsonDumpOptions(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        int optionOffset)
    {
        AnalyzeBooleanArgument(arguments, optionOffset, "skipkeys", "json.dumps(skipkeys=...) expects a bool.", diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, optionOffset + 1, "ensure_ascii", "json.dumps(ensure_ascii=...) expects a bool.", diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, optionOffset + 2, "check_circular", "json.dumps(check_circular=...) expects a bool.", diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, optionOffset + 3, "allow_nan", "json.dumps(allow_nan=...) expects a bool.", diagnostics, bindings);
        AnalyzeJsonClsNone(arguments, optionOffset + 4, "cls", diagnostics, bindings);
        AnalyzeJsonIndent(arguments, optionOffset + 5, "indent", diagnostics, bindings);
        AnalyzeJsonSeparators(arguments, optionOffset + 6, "separators", diagnostics, bindings);
        AnalyzeCallableOrNoneArgument(arguments, optionOffset + 7, "default", "json.dumps(default=...) expects a callable or None.", diagnostics, bindings);
        AnalyzeBooleanArgument(arguments, optionOffset + 8, "sort_keys", "json.dumps(sort_keys=...) expects a bool.", diagnostics, bindings);
    }

    private static bool AnalyzeJsonReadableFileArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.Unknown)
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.TextFileHandle)
        {
            var mode = (AbstractTextFileMode)value.Value;
            if (mode is AbstractTextFileMode.Write or AbstractTextFileMode.Append)
            {
                AddDiagnostic(diagnostics, "LA3109", "file is not open for reading.", expression.Span);
                return true;
            }

            return false;
        }

        AddDiagnostic(diagnostics, "LA3158", "json.load(fp, *, ...) expects a readable text file handle.", expression.Span);
        return true;
    }

    private static bool AnalyzeJsonWritableFileArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

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

        AddDiagnostic(diagnostics, "LA3158", "json.dump(obj, fp, *, ...) expects a writable text file handle.", expression.Span);
        return true;
    }

    private static void AnalyzeJsonClsNone(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value) ||
            value.Kind is AbstractValueKind.Unknown or AbstractValueKind.None)
        {
            return;
        }

        AddDiagnostic(diagnostics, "LA3158", "json cls=... custom encoder/decoder classes are not supported by Lython.", expression.Span);
    }

    private static void AnalyzeJsonIndent(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value) ||
            value.Kind == AbstractValueKind.Unknown ||
            value.Kind == AbstractValueKind.None ||
            value.IsStringLike ||
            StaticKnownCallArgumentChecks.IsRuntimeIntegerLike(value))
        {
            return;
        }

        AddDiagnostic(diagnostics, "LA3158", "json.dumps(indent=...) expects an integer, string, or None.", expression.Span);
    }

    private static void AnalyzeJsonSeparators(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value) ||
            value.Kind is AbstractValueKind.Unknown or AbstractValueKind.None)
        {
            return;
        }

        if (value.Kind is AbstractValueKind.Tuple or AbstractValueKind.List)
        {
            var items = (IReadOnlyList<AbstractValue>)value.Value;
            if (items.Count == 2 && items.All(static item => item.IsStringLike || StaticKnownCallArgumentChecks.IsUnknown(item)))
            {
                return;
            }
        }

        AddDiagnostic(diagnostics, "LA3158", "json.dumps(separators=...) expects a two-item tuple/list of strings or None.", expression.Span);
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

    private static bool AnalyzeCopyKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        var emitted = false;

        if (string.Equals(targetName, LythonKnownCallableSignatures.CopyCopy.Name, StringComparison.Ordinal))
        {
            return AnalyzeUnsupportedCopyProtocols(arguments, deep: false, diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.CopyReplace.Name, StringComparison.Ordinal))
        {
            return AnalyzeCopyReplaceDataclassFields(arguments, diagnostics, bindings);
        }

        if (!string.Equals(targetName, LythonKnownCallableSignatures.CopyDeepCopy.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (TryGetArgument(arguments, 1, "memo", bindings, out var memoExpression, out var memoValue) &&
            !IsUnknown(memoValue) &&
            memoValue.Kind is not (AbstractValueKind.None or AbstractValueKind.Dict))
        {
            AddDiagnostic(diagnostics, "LA3158", "copy.deepcopy(..., memo=...) expects a dict or None.", memoExpression.Span);
            emitted = true;
        }

        emitted |= AnalyzeUnsupportedCopyProtocols(arguments, deep: true, diagnostics, bindings);
        return emitted;
    }

    private static bool AnalyzeUnsupportedCopyProtocols(
        ConcreteCallArguments arguments,
        bool deep,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!TryGetArgument(arguments, 0, "x", bindings, out var expression, out var value) ||
            value.Kind != AbstractValueKind.UserInstance)
        {
            return false;
        }

        var instance = (AbstractInstanceSummary)value.Value;
        var hook = deep ? "__deepcopy__" : "__copy__";
        if (instance.Class.Methods.ContainsKey(hook) || instance.Class.FieldsByName.ContainsKey(hook))
        {
            return false;
        }

        foreach (var protocol in new[] { "__reduce_ex__", "__reduce__", "__getstate__", "__setstate__" })
        {
            if (instance.Class.Methods.ContainsKey(protocol) || instance.Class.FieldsByName.ContainsKey(protocol))
            {
                AddDiagnostic(diagnostics, "LA3158", $"copy protocol {protocol} is unsupported by Lython; define {hook} instead.", expression.Span);
                return true;
            }
        }

        return false;
    }

    private static bool AnalyzeCopyReplaceDataclassFields(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (arguments.Positional.Count == 0)
        {
            return false;
        }

        var value = arguments.ResolvePositionalValue(0, bindings);
        if (value.Kind != AbstractValueKind.UserInstance)
        {
            return false;
        }

        var instance = (AbstractInstanceSummary)value.Value;
        if (!instance.Class.IsDataclass)
        {
            return false;
        }

        foreach (var keyword in arguments.Keywords)
        {
            if (!instance.Class.FieldsByName.TryGetValue(keyword.Key, out var field))
            {
                AddDiagnostic(diagnostics, "LA3156", $"copy.replace() got an unexpected field '{keyword.Key}'.", keyword.Value.Span);
                return true;
            }

            if (!field.IncludeInInit)
            {
                AddDiagnostic(diagnostics, "LA3156", $"copy.replace() cannot override init=False field '{keyword.Key}'.", keyword.Value.Span);
                return true;
            }
        }

        return false;
    }

    private static bool AnalyzeRandomKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!targetName.StartsWith("random.", StringComparison.Ordinal))
        {
            return false;
        }

        var emitted = false;
        switch (targetName)
        {
            case "random.Random":
                return AnalyzeRandomSeedArgument(arguments, 0, "a", diagnostics, bindings);
            case "random.seed":
                emitted |= AnalyzeRandomSeedArgument(arguments, 0, "a", diagnostics, bindings);
                emitted |= AnalyzeIntegerArgument(arguments, 1, "version", "random.seed(..., version=...) expects an integer version.", diagnostics, bindings);
                return emitted;
            case "random.setstate":
                return false;
            case "random.randrange":
                emitted |= AnalyzeIntegerOrNoneArgument(arguments, 0, "start", "random.randrange(...) expects integer arguments.", diagnostics, bindings);
                emitted |= AnalyzeIntegerOrNoneArgument(arguments, 1, "stop", "random.randrange(...) expects integer arguments.", diagnostics, bindings);
                emitted |= AnalyzeIntegerOrNoneArgument(arguments, 2, "step", "random.randrange(...) expects integer arguments.", diagnostics, bindings);
                return emitted;
            case "random.randint":
                emitted |= AnalyzeIntegerArgument(arguments, 0, "a", "random.randint(a, b) expects integer bounds.", diagnostics, bindings);
                emitted |= AnalyzeIntegerArgument(arguments, 1, "b", "random.randint(a, b) expects integer bounds.", diagnostics, bindings);
                return emitted;
            case "random.choice":
                return AnalyzeIterableArgument(arguments, 0, "seq", "random.choice(seq) expects an iterable sequence.", diagnostics, bindings);
            case "random.choices":
                AnalyzeRandomChoicesCall(arguments, diagnostics, bindings);
                return true;
            case "random.shuffle":
                return AnalyzeMutableSequenceArgument(arguments, 0, "x", "random.shuffle(x) expects a mutable sequence.", diagnostics, bindings);
            case "random.sample":
                emitted |= AnalyzeIterableArgument(arguments, 0, "population", "random.sample(population, k, *, counts=None) expects an iterable population.", diagnostics, bindings);
                emitted |= AnalyzeIntegerArgument(arguments, 1, "k", "random.sample(..., k=...) expects an integer.", diagnostics, bindings);
                emitted |= AnalyzeIterableOrNoneArgument(arguments, 2, "counts", "random.sample(..., counts=...) expects an iterable of counts or None.", diagnostics, bindings);
                return emitted;
            case "random.getrandbits":
                return AnalyzeIntegerArgument(arguments, 0, "k", "random.getrandbits(k) expects an integer.", diagnostics, bindings);
            case "random.randbytes":
                return AnalyzeIntegerArgument(arguments, 0, "n", "random.randbytes(n) expects an integer.", diagnostics, bindings);
            case "random.uniform":
                emitted |= AnalyzeRealArgument(arguments, 0, "a", "random.uniform(a, b) expects real numbers.", diagnostics, bindings);
                emitted |= AnalyzeRealArgument(arguments, 1, "b", "random.uniform(a, b) expects real numbers.", diagnostics, bindings);
                return emitted;
            case "random.triangular":
                emitted |= AnalyzeRealOrNoneArgument(arguments, 0, "low", "random.triangular(..., low=...) expects a real number or None.", diagnostics, bindings);
                emitted |= AnalyzeRealOrNoneArgument(arguments, 1, "high", "random.triangular(..., high=...) expects a real number or None.", diagnostics, bindings);
                emitted |= AnalyzeRealOrNoneArgument(arguments, 2, "mode", "random.triangular(..., mode=...) expects a real number or None.", diagnostics, bindings);
                return emitted;
            case "random.expovariate":
            case "random.gauss":
            case "random.normalvariate":
                emitted |= AnalyzeRealOrNoneArgument(arguments, 0, targetName.EndsWith("expovariate", StringComparison.Ordinal) ? "lambd" : "mu", $"{targetName}(...) expects real arguments.", diagnostics, bindings);
                emitted |= AnalyzeRealOrNoneArgument(arguments, 1, "sigma", $"{targetName}(...) expects real arguments.", diagnostics, bindings);
                return emitted;
            case "random.betavariate":
            case "random.gammavariate":
            case "random.lognormvariate":
            case "random.weibullvariate":
            case "random.vonmisesvariate":
                emitted |= AnalyzeRealArgument(arguments, 0, FirstRandomDistributionParameter(targetName), $"{targetName}(...) expects real arguments.", diagnostics, bindings);
                emitted |= AnalyzeRealArgument(arguments, 1, SecondRandomDistributionParameter(targetName), $"{targetName}(...) expects real arguments.", diagnostics, bindings);
                return emitted;
            case "random.paretovariate":
                return AnalyzeRealArgument(arguments, 0, "alpha", "random.paretovariate(alpha) expects a real argument.", diagnostics, bindings);
            default:
                return false;
        }
    }

    private static void AnalyzeRandomMemberCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
        => AnalyzeRandomKnownCallArgumentTypes("random." + memberName, arguments, diagnostics, bindings);

    private static void AnalyzeRandomChoicesCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        AnalyzeIterableArgument(arguments, 0, "population", "random.choices(population, ...) expects an iterable population.", diagnostics, bindings);

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

    private static bool AnalyzeRandomSeedArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, "random seed expects None, bool, int, float, str, or bytes.", diagnostics, bindings, static value =>
            value.Kind is AbstractValueKind.None or
                AbstractValueKind.Boolean or
                AbstractValueKind.BooleanType or
                AbstractValueKind.Integer or
                AbstractValueKind.IntegerType or
                AbstractValueKind.Float or
                AbstractValueKind.FloatType or
                AbstractValueKind.String or
                AbstractValueKind.StringType or
                AbstractValueKind.Bytes or
                AbstractValueKind.BytesType);

    private static bool AnalyzeIterableArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => !StaticAbstractFacts.IsDefinitelyNonIterable(value));

    private static bool AnalyzeIterableOrNoneArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || !StaticAbstractFacts.IsDefinitelyNonIterable(value));

    private static bool AnalyzeMutableSequenceArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value =>
            value.Kind is AbstractValueKind.List or AbstractValueKind.ListType || StaticKnownCallArgumentChecks.IsUnknown(value));

    private static bool AnalyzeRealArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, StaticAbstractFacts.IsNumericLike);

    private static bool AnalyzeRealOrNoneArgument(ConcreteCallArguments arguments, int position, string keyword, string message, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeArgument(arguments, position, keyword, message, diagnostics, bindings, static value => value.Kind == AbstractValueKind.None || StaticAbstractFacts.IsNumericLike(value));

    private static string FirstRandomDistributionParameter(string targetName)
        => targetName switch
        {
            "random.betavariate" or "random.gammavariate" or "random.paretovariate" or "random.weibullvariate" => "alpha",
            "random.vonmisesvariate" or "random.lognormvariate" => "mu",
            _ => "a"
        };

    private static string SecondRandomDistributionParameter(string targetName)
        => targetName switch
        {
            "random.betavariate" or "random.gammavariate" or "random.weibullvariate" => "beta",
            "random.vonmisesvariate" => "kappa",
            "random.lognormvariate" => "sigma",
            _ => "b"
        };

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

    private static bool AnalyzePkgutilKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilModuleInfo.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeStringArgument(arguments, 1, "name", "pkgutil.ModuleInfo(..., name, ...) expects a string name.", diagnostics, bindings);
            emitted |= AnalyzeBooleanArgument(arguments, 2, "ispkg", "pkgutil.ModuleInfo(..., ispkg) expects a bool.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilIterModules.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePkgutilPathArgument(arguments, 0, "path", "pkgutil.iter_modules(..., path=...) expects None, a path string, Path, or iterable of path strings.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "prefix", "pkgutil.iter_modules(..., prefix=...) expects a string.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilWalkPackages.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePkgutilPathArgument(arguments, 0, "path", "pkgutil.walk_packages(..., path=...) expects None, a path string, Path, or iterable of path strings.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "prefix", "pkgutil.walk_packages(..., prefix=...) expects a string.", diagnostics, bindings);
            emitted |= AnalyzeCallableOrNoneArgument(arguments, 2, "onerror", "pkgutil.walk_packages(..., onerror=...) expects a callable or None.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilFindLoader.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.PkgutilResolveName.Name, StringComparison.Ordinal))
        {
            var owner = targetName.EndsWith("resolve_name", StringComparison.Ordinal) ? "pkgutil.resolve_name" : "pkgutil.find_loader";
            return AnalyzeStringArgument(arguments, 0, targetName.EndsWith("resolve_name", StringComparison.Ordinal) ? "name" : "fullname", $"{owner}(...) expects a module name string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilGetLoader.Name, StringComparison.Ordinal))
        {
            return AnalyzeArgument(
                arguments,
                0,
                "module_or_name",
                "pkgutil.get_loader(module_or_name) expects a module object or module name string.",
                diagnostics,
                bindings,
                static value => value.IsStringLike || value.Kind == AbstractValueKind.Module);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilExtendPath.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzePkgutilPathArgument(arguments, 0, "path", "pkgutil.extend_path(path, name) expects None, a path string, Path, or iterable of path strings.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "name", "pkgutil.extend_path(path, name) expects a package name string.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilGetData.Name, StringComparison.Ordinal))
        {
            var emitted = AnalyzeStringArgument(arguments, 0, "package", "pkgutil.get_data(package, resource) expects a string package name.", diagnostics, bindings);
            emitted |= AnalyzeStringArgument(arguments, 1, "resource", "pkgutil.get_data(package, resource) expects a string resource name.", diagnostics, bindings);
            return emitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilIterImporters.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 0, "fullname", "pkgutil.iter_importers([fullname]) expects a module name string.", diagnostics, bindings);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.PkgutilIterImporterModules.Name, StringComparison.Ordinal) ||
            string.Equals(targetName, LythonKnownCallableSignatures.PkgutilIterZipimportModules.Name, StringComparison.Ordinal))
        {
            return AnalyzeStringArgument(arguments, 1, "prefix", "pkgutil importer helpers expect prefix to be a string.", diagnostics, bindings);
        }

        return false;
    }

    private static bool AnalyzePkgutilPathArgument(
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

        if (IsUnknown(value) || value.Kind == AbstractValueKind.None || IsPathLike(value))
        {
            return false;
        }

        if (value.Kind is AbstractValueKind.List or AbstractValueKind.Tuple or AbstractValueKind.Set)
        {
            foreach (var item in (IReadOnlyList<AbstractValue>)value.Value)
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

    private static void AnalyzePkgutilModuleInfoMemberCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (memberName)
        {
            case "_replace":
                AnalyzeStringArgument(arguments, 1, "name", "ModuleInfo._replace(..., name=...) expects a string.", diagnostics, bindings);
                AnalyzeBooleanArgument(arguments, 2, "ispkg", "ModuleInfo._replace(..., ispkg=...) expects a bool.", diagnostics, bindings);
                break;
            case "index":
                AnalyzeIntegerArgument(arguments, 1, "start", "ModuleInfo.index(value[, start[, stop]]) expects integer start/stop bounds.", diagnostics, bindings);
                AnalyzeIntegerArgument(arguments, 2, "stop", "ModuleInfo.index(value[, start[, stop]]) expects integer start/stop bounds.", diagnostics, bindings);
                break;
        }
    }

    private static void AnalyzePkgutilLoaderMemberCall(
        string memberName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        switch (memberName)
        {
            case "is_package":
                AnalyzeStringOrNoneArgument(arguments, 0, "fullname", "loader.is_package([fullname]) expects a string or None.", diagnostics, bindings);
                break;
            case "get_source":
                AnalyzeStringOrNoneArgument(arguments, 0, "fullname", "loader.get_source([fullname]) expects a string or None.", diagnostics, bindings);
                break;
        }
    }

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

        if (value.Kind is AbstractValueKind.ListType or AbstractValueKind.SetType)
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
