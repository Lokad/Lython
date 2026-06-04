namespace Lokad.Lython.Frontend;

internal static class StaticTextIoContractFamily
{
    private const string TextBoundaryCode = "LA3046";
    private const string TextBoundaryMessage = "Text-only host APIs do not accept bytes; Lython host boundaries are UTF-8 text-shaped only.";

    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is IdentifierExpressionSyntax { Name: "open" })
        {
            AnalyzeOpenCall(arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is not MemberExpressionSyntax member)
        {
            return false;
        }

        if (member.MemberName == "write_text")
        {
            AnalyzeTextBoundaryStringArgument(arguments, 0, "text", TextBoundaryCode, TextBoundaryMessage, diagnostics, bindings);
            if (StaticAbstractValueResolver.TryResolveKnownPath(member.Target, bindings))
            {
                AnalyzePathWriteTextCall(arguments, diagnostics, bindings);
            }

            return true;
        }

        if (member.MemberName == "read_text" &&
            StaticAbstractValueResolver.TryResolveKnownPath(member.Target, bindings))
        {
            AnalyzePathReadTextCall(arguments, diagnostics, bindings);
            return true;
        }

        if (member.MemberName == "open" &&
            StaticAbstractValueResolver.TryResolveKnownPath(member.Target, bindings))
        {
            AnalyzePathOpenCall(arguments, diagnostics, bindings);
            return true;
        }

        if (member.MemberName is "read" or "readline" or "readlines" &&
            StaticAbstractValueResolver.TryResolveKnownTextFileHandle(member.Target, bindings, out var readMode))
        {
            AnalyzeTextFileReadCall(member.MemberName, readMode, arguments, diagnostics, call.Span);
            return true;
        }

        if (member.MemberName is "write" or "writelines" &&
            StaticAbstractValueResolver.TryResolveKnownTextFileHandle(member.Target, bindings, out var writeMode))
        {
            AnalyzeTextFileWriteCall(member.MemberName, writeMode, arguments, diagnostics, bindings, call.Span);
            return true;
        }

        if (member.MemberName is "write_bytes" or "read_bytes" &&
            StaticAbstractValueResolver.TryResolveKnownPath(member.Target, bindings))
        {
            AddDiagnostic(
                diagnostics,
                "LA3047",
                $"Path.{member.MemberName}(...) is not supported by Lython. The host boundary is UTF-8 text-shaped only.",
                member.Span,
                new StaticDiagnosticProof("contract", $"Path.{member.MemberName}", "binary path API is outside the Lython text boundary"));
            return true;
        }

        return false;
    }

    private static void AnalyzeOpenCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(1, "mode", out var modeExpression))
        {
            if (modeExpression is not NoneLiteralExpressionSyntax &&
                !StaticAbstractValueResolver.TryResolveKnownString(modeExpression, bindings, out _))
            {
                if (StaticAbstractValueResolver.IsDefinitelyKnownNonStringLiteral(modeExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3000", "open(..., mode=...) must be a string literal or None when it is statically known.", modeExpression.Span);
                }
            }
            else if (modeExpression is not NoneLiteralExpressionSyntax &&
                     StaticAbstractValueResolver.TryResolveKnownString(modeExpression, bindings, out var modeText))
            {
                if (modeText.Contains('b', StringComparison.Ordinal))
                {
                    AddDiagnostic(diagnostics, "LA3001", "open() only supports UTF-8 text modes; binary modes like 'rb' and 'wb' are unsupported.", modeExpression.Span);
                }
                else if (modeText is not ("r" or "w" or "a"))
                {
                    AddDiagnostic(diagnostics, "LA3002", "open() only supports modes 'r', 'w', and 'a'.", modeExpression.Span);
                }
            }
        }

        AnalyzeEncodingArgument(arguments, 2, "encoding", "LA3003", "open(..., encoding=...) must be 'utf-8', 'utf-8-sig', or None when it is statically known.", "open() only supports encoding='utf-8' or 'utf-8-sig'.", diagnostics, bindings);
        AnalyzeNewlineArgument(arguments, 3, "newline", "LA3004", "open(..., newline=...) must be '' or None when it is statically known.", "open() only supports newline=''.", diagnostics, bindings);

        if (arguments.TryGetValue(4, "errors", out var errorsExpression))
        {
            if (errorsExpression is not NoneLiteralExpressionSyntax &&
                !StaticAbstractValueResolver.TryResolveKnownString(errorsExpression, bindings, out _))
            {
                if (StaticAbstractValueResolver.IsDefinitelyKnownLiteral(errorsExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3005", "open(..., errors=...) must be 'strict' or None when it is statically known.", errorsExpression.Span);
                }
            }
            else if (errorsExpression is not NoneLiteralExpressionSyntax &&
                     StaticAbstractValueResolver.TryResolveKnownString(errorsExpression, bindings, out var errorsText) &&
                     !errorsText.Equals("strict", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnostic(diagnostics, "LA3005", "open() only supports errors='strict'.", errorsExpression.Span);
            }
        }
    }

    private static void AnalyzePathReadTextCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        AnalyzeEncodingArgument(
            arguments,
            0,
            "encoding",
            "LA3049",
            "Path.read_text() only supports encoding='utf-8' or 'utf-8-sig'.",
            "Path.read_text() only supports encoding='utf-8' or 'utf-8-sig'.",
            diagnostics,
            bindings);
        AnalyzeErrorsArgument(
            arguments,
            1,
            "errors",
            "LA3049",
            "Path.read_text() only supports errors='strict'.",
            "Path.read_text() only supports errors='strict'.",
            diagnostics,
            bindings);
    }

    private static void AnalyzePathWriteTextCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "text", out var textExpression) &&
            !StaticAbstractValueResolver.TryResolveKnownString(textExpression, bindings, out _) &&
            !StaticAbstractValueResolver.IsDefinitelyKnownBytesLiteral(textExpression, bindings) &&
            (StaticAbstractValueResolver.IsDefinitelyKnownLiteral(textExpression, bindings) ||
             StaticAbstractValueResolver.IsDefinitelyKnownNonStringLike(textExpression, bindings)))
        {
            AddDiagnostic(diagnostics, "LA3072", "Path.write_text(text[, encoding][, errors][, newline]) expects a string plus optional keyword-compatible arguments.", textExpression.Span);
        }

        AnalyzeEncodingArgument(
            arguments,
            1,
            "encoding",
            "LA3053",
            "Path.write_text() only supports encoding='utf-8' or 'utf-8-sig'.",
            "Path.write_text() only supports encoding='utf-8' or 'utf-8-sig'.",
            diagnostics,
            bindings);
        AnalyzeErrorsArgument(
            arguments,
            2,
            "errors",
            "LA3053",
            "Path.write_text() only supports errors='strict'.",
            "Path.write_text() only supports errors='strict'.",
            diagnostics,
            bindings);
        AnalyzeNewlineArgument(
            arguments,
            3,
            "newline",
            "LA3054",
            "Path.write_text() only supports newline=''.",
            "Path.write_text() only supports newline=''.",
            diagnostics,
            bindings);
    }

    private static void AnalyzePathOpenCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "mode", out var modeExpression))
        {
            if (modeExpression is not NoneLiteralExpressionSyntax &&
                !StaticAbstractValueResolver.TryResolveKnownString(modeExpression, bindings, out _))
            {
                if (StaticAbstractValueResolver.IsDefinitelyKnownLiteral(modeExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3060", "Path.open(mode) expects mode to be a string.", modeExpression.Span);
                }
            }
            else if (modeExpression is not NoneLiteralExpressionSyntax &&
                     StaticAbstractValueResolver.TryResolveKnownString(modeExpression, bindings, out var modeText))
            {
                if (modeText.Contains('b', StringComparison.Ordinal))
                {
                    AddDiagnostic(diagnostics, "LA3061", "Path.open() only supports UTF-8 text modes; binary modes like 'rb' and 'wb' are unsupported.", modeExpression.Span);
                }
                else if (modeText is not ("r" or "w" or "a"))
                {
                    AddDiagnostic(diagnostics, "LA3062", "Path.open() only supports modes 'r', 'w', and 'a'.", modeExpression.Span);
                }
            }
        }

        AnalyzeEncodingArgument(
            arguments,
            1,
            "encoding",
            "LA3063",
            "Path.open() only supports encoding='utf-8' or 'utf-8-sig'.",
            "Path.open() only supports encoding='utf-8' or 'utf-8-sig'.",
            diagnostics,
            bindings);
        AnalyzeErrorsArgument(
            arguments,
            2,
            "errors",
            "LA3063",
            "Path.open() only supports errors='strict'.",
            "Path.open() only supports errors='strict'.",
            diagnostics,
            bindings);
        AnalyzeNewlineArgument(
            arguments,
            3,
            "newline",
            "LA3064",
            "Path.open() only supports newline=''.",
            "Path.open() only supports newline=''.",
            diagnostics,
            bindings);
    }

    private static void AnalyzeTextFileReadCall(
        string memberName,
        AbstractTextFileMode mode,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        LythonSourceSpan span)
    {
        if (arguments.Positional.Count != 0 || arguments.Keywords.Count != 0)
        {
            AddDiagnostic(diagnostics, "LA3108", $"file.{memberName}() expects no arguments.", span);
            return;
        }

        if (mode is AbstractTextFileMode.Write or AbstractTextFileMode.Append)
        {
            AddDiagnostic(
                diagnostics,
                "LA3109",
                "file is not open for reading.",
                span,
                new StaticDiagnosticProof("state", "text file mode", "read operation on a write/append-mode handle", AbstractValue.TextFileHandle(mode, span)));
        }
    }

    private static void AnalyzeTextFileWriteCall(
        string memberName,
        AbstractTextFileMode mode,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        LythonSourceSpan span)
    {
        if (mode == AbstractTextFileMode.Read)
        {
            AddDiagnostic(
                diagnostics,
                "LA3110",
                "file is not open for writing.",
                span,
                new StaticDiagnosticProof("state", "text file mode", "write operation on a read-mode handle", AbstractValue.TextFileHandle(mode, span)));
            return;
        }

        if (memberName == "write")
        {
            AnalyzeTextFileWriteArgument(arguments, diagnostics, bindings, span);
            return;
        }

        AnalyzeTextFileWriteLinesArgument(arguments, diagnostics, bindings, span);
    }

    private static void AnalyzeTextFileWriteArgument(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        LythonSourceSpan span)
    {
        if (!arguments.TryGetValue(0, "text", out var textExpression))
        {
            AddDiagnostic(diagnostics, "LA3111", "file.write(text) expects one string argument.", span);
            return;
        }

        if (arguments.Positional.Count + arguments.Keywords.Count != 1)
        {
            AddDiagnostic(diagnostics, "LA3111", "file.write(text) expects one string argument.", span);
            return;
        }

        if (!StaticAbstractValueResolver.TryResolveKnownStringLike(textExpression, bindings) &&
            (StaticAbstractValueResolver.IsDefinitelyKnownLiteral(textExpression, bindings) ||
             StaticAbstractValueResolver.IsDefinitelyKnownNonStringLike(textExpression, bindings)))
        {
            AddDiagnostic(diagnostics, "LA3111", "file.write(text) expects one string argument.", textExpression.Span);
        }
    }

    private static void AnalyzeTextFileWriteLinesArgument(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        LythonSourceSpan span)
    {
        if (!arguments.TryGetValue(0, "lines", out var linesExpression))
        {
            AddDiagnostic(diagnostics, "LA3112", "file.writelines(lines) expects one iterable of strings argument.", span);
            return;
        }

        if (arguments.Positional.Count + arguments.Keywords.Count != 1)
        {
            AddDiagnostic(diagnostics, "LA3112", "file.writelines(lines) expects one iterable of strings argument.", span);
            return;
        }

        if (StaticAbstractValueResolver.TryResolveKnownSequenceItems(linesExpression, bindings, out var sequenceItems))
        {
            StaticContractChecks.AnalyzeIterableOfStringsLiteral(sequenceItems, "LA3112", "file.writelines(lines) expects an iterable of strings.", diagnostics);
        }
        else if (StaticAbstractValueResolver.TryResolve(linesExpression, bindings, out var linesValue) &&
                 linesValue.Kind == AbstractValueKind.ListType)
        {
            var item = (AbstractValue)linesValue.Value;
            if (!item.IsStringLike)
            {
                AddDiagnostic(diagnostics, "LA3112", "file.writelines(lines) expects an iterable of strings.", linesExpression.Span);
            }
        }
        else if (StaticAbstractValueResolver.IsDefinitelyKnownNonIterableLiteral(linesExpression, bindings))
        {
            AddDiagnostic(diagnostics, "LA3112", "file.writelines(lines) expects an iterable of strings.", linesExpression.Span);
        }
    }

    private static void AnalyzeEncodingArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string knownLiteralMessage,
        string unsupportedEncodingMessage,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var encodingExpression))
        {
            return;
        }

        if (encodingExpression is not NoneLiteralExpressionSyntax &&
            !StaticAbstractValueResolver.TryResolveKnownString(encodingExpression, bindings, out _))
        {
            if (StaticAbstractValueResolver.IsDefinitelyKnownLiteral(encodingExpression, bindings))
            {
                AddDiagnostic(diagnostics, code, knownLiteralMessage, encodingExpression.Span);
            }

            return;
        }

        if (encodingExpression is not NoneLiteralExpressionSyntax &&
            StaticAbstractValueResolver.TryResolveKnownString(encodingExpression, bindings, out var encodingText) &&
            !encodingText.Equals("utf-8", StringComparison.OrdinalIgnoreCase) &&
            !encodingText.Equals("utf-8-sig", StringComparison.OrdinalIgnoreCase))
        {
            AddDiagnostic(diagnostics, code, unsupportedEncodingMessage, encodingExpression.Span);
        }
    }

    private static void AnalyzeNewlineArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string knownLiteralMessage,
        string unsupportedNewlineMessage,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var newlineExpression))
        {
            return;
        }

        if (newlineExpression is not NoneLiteralExpressionSyntax &&
            !StaticAbstractValueResolver.TryResolveKnownString(newlineExpression, bindings, out _))
        {
            if (StaticAbstractValueResolver.IsDefinitelyKnownLiteral(newlineExpression, bindings))
            {
                AddDiagnostic(diagnostics, code, knownLiteralMessage, newlineExpression.Span);
            }
        }
        else if (newlineExpression is not NoneLiteralExpressionSyntax &&
                 StaticAbstractValueResolver.TryResolveKnownString(newlineExpression, bindings, out var newlineText) &&
                 newlineText.Length != 0)
        {
            AddDiagnostic(diagnostics, code, unsupportedNewlineMessage, newlineExpression.Span);
        }
    }

    private static void AnalyzeErrorsArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string knownLiteralMessage,
        string unsupportedErrorsMessage,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var errorsExpression))
        {
            return;
        }

        if (errorsExpression is not NoneLiteralExpressionSyntax &&
            !StaticAbstractValueResolver.TryResolveKnownString(errorsExpression, bindings, out _))
        {
            if (StaticAbstractValueResolver.IsDefinitelyKnownLiteral(errorsExpression, bindings))
            {
                AddDiagnostic(diagnostics, code, knownLiteralMessage, errorsExpression.Span);
            }

            return;
        }

        if (errorsExpression is not NoneLiteralExpressionSyntax &&
            StaticAbstractValueResolver.TryResolveKnownString(errorsExpression, bindings, out var errorsText) &&
            !errorsText.Equals("strict", StringComparison.OrdinalIgnoreCase))
        {
            AddDiagnostic(diagnostics, code, unsupportedErrorsMessage, errorsExpression.Span);
        }
    }

    private static void AnalyzeTextBoundaryStringArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
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
            AddDiagnostic(
                diagnostics,
                code,
                message,
                expression.Span,
                new StaticDiagnosticProof("contract", keyword, "bytes cannot cross a text-only host boundary"));
        }
    }

    private static void AddDiagnostic(
        List<LythonDiagnostic> diagnostics,
        string code,
        string message,
        LythonSourceSpan span,
        StaticDiagnosticProof? proof = null)
        => StaticDiagnosticSink.AddError(diagnostics, code, message, span, proof);
}
