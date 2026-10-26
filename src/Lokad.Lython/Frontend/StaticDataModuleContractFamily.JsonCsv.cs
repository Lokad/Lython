using System.Globalization;
using System.Text;
using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static partial class StaticDataModuleContractFamily
{
    private static bool AnalyzeCsvReaderCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var emitted = AnalyzeCsvInputArgument(arguments, 0, "csvfile", "csv.reader(csvfile) expects an iterable of strings or a readable text file handle.", diagnostics, bindings);
        AnalyzeCsvOptions(arguments, CsvOptionArgumentLayout.Standard, diagnostics, bindings);
        return emitted;
    }

    private static bool AnalyzeCsvWriterCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var emitted = AnalyzeOptionalCsvWriterFileArgument(arguments, 0, "fileobj", diagnostics, bindings);
        AnalyzeCsvOptions(arguments, CsvOptionArgumentLayout.Standard, diagnostics, bindings);
        return emitted;
    }

    private static bool AnalyzeCsvDictReaderCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var emitted = AnalyzeCsvInputArgument(arguments, 0, "f", "csv.DictReader(f) expects an iterable of strings or a readable text file handle.", diagnostics, bindings);
        AnalyzeCsvOptions(arguments, CsvOptionArgumentLayout.Dictionary, diagnostics, bindings);
        return emitted;
    }

    private static bool AnalyzeCsvDictWriterCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var emitted = AnalyzeRequiredCsvWriterFileArgument(arguments, 0, "f", diagnostics, bindings);
        emitted |= AnalyzeDictWriterExtrasAction(arguments, diagnostics, bindings);
        AnalyzeCsvOptions(arguments, CsvOptionArgumentLayout.Dictionary, diagnostics, bindings);
        return emitted;
    }

    private static bool AnalyzeJsonReadableFileArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (value.Kind is AbstractValueKind.Unknown or AbstractValueKind.Never or AbstractValueKind.MaybeNone
            or AbstractValueKind.UserInstance or AbstractValueKind.UserClass or AbstractValueKind.StringIO)
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.TextFileHandle)
        {
            var mode = value.RequireTextFileMode();
            if (mode is AbstractTextFileMode.Write or AbstractTextFileMode.Append)
            {
                AddDiagnostic(diagnostics, "LA3109", "file is not open for reading.", expression.Span);
                return true;
            }

            return false;
        }

        AddDiagnostic(diagnostics, "LA3158", "json.load(fp, *, ...) expects a text stream with read().", expression.Span);
        return true;
    }

    private static bool AnalyzeJsonWritableFileArgument(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (!TryGetArgument(arguments, position, keyword, bindings, out var expression, out var value))
        {
            return false;
        }

        if (value.Kind is AbstractValueKind.Unknown or AbstractValueKind.Never or AbstractValueKind.MaybeNone
            or AbstractValueKind.UserInstance or AbstractValueKind.UserClass or AbstractValueKind.StringIO)
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.TextFileHandle)
        {
            var mode = value.RequireTextFileMode();
            if (mode == AbstractTextFileMode.Read)
            {
                AddDiagnostic(diagnostics, "LA3111", "file is not open for writing.", expression.Span);
                return true;
            }

            return false;
        }

        AddDiagnostic(diagnostics, "LA3158", "json.dump(obj, fp, *, ...) expects a text stream with write().", expression.Span);
        return true;
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
        if (value.Kind is AbstractValueKind.Unknown or AbstractValueKind.Never or AbstractValueKind.MaybeNone or
            AbstractValueKind.UserInstance or AbstractValueKind.UserClass or AbstractValueKind.StringIO)
        {
            return false;
        }

        if (value.Kind == AbstractValueKind.TextFileHandle)
        {
            var mode = value.RequireTextFileMode();
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

        AddDiagnostic(diagnostics, "LA3067", "csv writer expects an object with a callable write method.", expression.Span);
        return true;
    }

    private static void AnalyzeCsvOptions(
        ConcreteCallArguments arguments,
        CsvOptionArgumentLayout layout,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        static void AnalyzeDialect(
            ConcreteCallArguments arguments,
            int position,
            List<LythonDiagnostic> diagnostics,
            AbstractState bindings)
        {
            if (!arguments.TryGetValue(position, "dialect", out var dialectExpression) ||
                dialectExpression is NoneLiteralExpressionSyntax)
            {
                return;
            }

            if (StaticAbstractValueResolver.TryResolveKnownString(dialectExpression, bindings, out var text) &&
                string.Equals(text, "excel", StringComparison.Ordinal))
            {
                return;
            }

            if (StaticAbstractFacts.IsDefinitelyKnownLiteral(dialectExpression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3067", "csv dialect registry is unsupported; pass explicit CSV options instead.", dialectExpression.Span);
            }
        }

        AnalyzeDialect(arguments, layout.Dialect, diagnostics, bindings);
        AnalyzeCsvCharacterOption(arguments, layout.Delimiter, "delimiter", allowNone: false, diagnostics, bindings);
        AnalyzeCsvCharacterOption(arguments, layout.QuoteCharacter, "quotechar", allowNone: true, diagnostics, bindings);
        AnalyzeCsvQuoting(arguments, layout.Quoting, diagnostics, bindings);
        AnalyzeCsvCharacterOption(arguments, layout.EscapeCharacter, "escapechar", allowNone: true, diagnostics, bindings);
        AnalyzeStringArgument(arguments, layout.LineTerminator, "lineterminator", "csv lineterminator must be a string.", diagnostics, bindings);
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

        var runes = text.EnumerateRunes();
        if (!runes.MoveNext() || runes.MoveNext())
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

        if (!string.Equals(text, "raise", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(text, "ignore", StringComparison.OrdinalIgnoreCase))
        {
            AddDiagnostic(diagnostics, "LA3067", "csv.DictWriter(..., extrasaction=...) expects 'raise' or 'ignore'.", expression.Span);
            return true;
        }

        return false;
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

}
