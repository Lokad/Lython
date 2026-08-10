namespace Lokad.Lython.Frontend;

internal static class StaticTextIoContractFamily
{
    private const string TextBoundaryCode = "LA3046";
    private const string TextBoundaryMessage = "Text-only host APIs do not accept bytes; Lython host boundaries are UTF-8 text-shaped only.";
    private const string OpenSignature = "open(file/path[, mode][, buffering][, encoding][, errors][, newline][, closefd][, opener])";

    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is IdentifierExpressionSyntax { Name: "open" })
        {
            AnalyzeOpenCall(call.Span, arguments, diagnostics, bindings);
            return true;
        }

        if (call.Target is not MemberExpressionSyntax member)
        {
            return false;
        }

        if (member.MemberName == "write_text")
        {
            StaticContractChecks.AnalyzeTextBoundaryStringArgument(arguments, 0, "text", TextBoundaryCode, TextBoundaryMessage, diagnostics, bindings);
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

    private static void AnalyzeOpenCall(LythonSourceSpan callSpan, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        AnalyzeOpenCallShape(callSpan, arguments, diagnostics);

        if (arguments.TryGetValue(1, "mode", out var modeExpression))
        {
            if (modeExpression is NoneLiteralExpressionSyntax)
            {
                AddDiagnostic(diagnostics, "LA3000", "open(file/path, mode) expects mode to be a string; omit mode to use read mode.", modeExpression.Span);
            }
            else if (!StaticAbstractValueResolver.TryResolveKnownString(modeExpression, bindings, out _))
            {
                if (StaticAbstractValueResolver.IsDefinitelyKnownNonStringLiteral(modeExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3000", "open(..., mode=...) must be a string literal when it is statically known.", modeExpression.Span);
                }
            }
            else if (StaticAbstractValueResolver.TryResolveKnownString(modeExpression, bindings, out var modeText))
            {
                if (modeText.Contains('b', StringComparison.Ordinal))
                {
                    AddDiagnostic(diagnostics, "LA3001", "open() only supports text modes; binary modes like 'rb' and 'wb' are unsupported.", modeExpression.Span);
                }
                else if (modeText.Contains('+', StringComparison.Ordinal))
                {
                    AddDiagnostic(diagnostics, "LA3002", "open() does not support updating text modes such as 'r+'.", modeExpression.Span);
                }
                else if (!IsSupportedTextMode(modeText))
                {
                    AddDiagnostic(diagnostics, "LA3002", "open() only supports modes 'r', 'w', and 'a' with optional text marker 't'.", modeExpression.Span);
                }
            }
        }

        StaticKnownCallArgumentChecks.AnalyzeIntegerOrNoneArgument(arguments, 2, "buffering", "open(..., buffering=...) expects an integer or None.", diagnostics, bindings);
        AnalyzeEncodingArgument(arguments, 3, "encoding", "LA3003", "open(..., encoding=...) must be 'utf-8', 'utf-8-sig', 'latin-1', or None when it is statically known.", "open() only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.", diagnostics, bindings);
        AnalyzeErrorsArgument(arguments, 4, "errors", "LA3005", "open(..., errors=...) must be 'strict', 'ignore', 'replace', 'backslashreplace', or None when it is statically known.", "open() only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.", diagnostics, bindings);
        AnalyzeNewlineArgument(arguments, 5, "newline", "LA3004", "open(..., newline=...) must be None, '', '\\n', '\\r', or '\\r\\n' when it is statically known.", "open() newline must be None, '', '\\n', '\\r', or '\\r\\n'.", diagnostics, bindings);
        StaticKnownCallArgumentChecks.AnalyzeBooleanOrNoneArgument(arguments, 6, "closefd", "open(..., closefd=...) expects a bool or None.", diagnostics, bindings);
        AnalyzeCloseFdArgument(arguments, diagnostics, bindings);
        AnalyzeOpenerArgument(arguments, diagnostics, bindings);
    }

    private static void AnalyzeOpenCallShape(LythonSourceSpan callSpan, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics)
    {
        if (arguments.Positional.Count > 8)
        {
            AddDiagnostic(diagnostics, "LA3151", $"{OpenSignature} received too many positional arguments.", arguments.Positional[8].Span);
        }

        var hasFileKeyword = arguments.Keywords.ContainsKey("file");
        var hasPathKeyword = arguments.Keywords.ContainsKey("path");
        if (arguments.Positional.Count == 0 && !hasFileKeyword && !hasPathKeyword)
        {
            AddDiagnostic(diagnostics, "LA3151", $"{OpenSignature} expects a file/path argument.", callSpan);
        }

        if ((arguments.Positional.Count > 0 && (hasFileKeyword || hasPathKeyword)) ||
            (hasFileKeyword && hasPathKeyword))
        {
            AddDiagnostic(diagnostics, "LA3151", $"{OpenSignature} got multiple values for argument 'file/path'.", callSpan);
        }

        AnalyzeOpenDuplicate(arguments, 1, "mode", diagnostics);
        AnalyzeOpenDuplicate(arguments, 2, "buffering", diagnostics);
        AnalyzeOpenDuplicate(arguments, 3, "encoding", diagnostics);
        AnalyzeOpenDuplicate(arguments, 4, "errors", diagnostics);
        AnalyzeOpenDuplicate(arguments, 5, "newline", diagnostics);
        AnalyzeOpenDuplicate(arguments, 6, "closefd", diagnostics);
        AnalyzeOpenDuplicate(arguments, 7, "opener", diagnostics);

        foreach (var keyword in arguments.Keywords.Keys)
        {
            if (keyword is "file" or "path" or "mode" or "buffering" or "encoding" or "errors" or "newline" or "closefd" or "opener")
            {
                continue;
            }

            AddDiagnostic(diagnostics, "LA3151", $"{OpenSignature} got an unexpected keyword argument '{keyword}'.", arguments.Keywords[keyword].Span);
        }
    }

    private static void AnalyzeOpenDuplicate(ConcreteCallArguments arguments, int position, string keyword, List<LythonDiagnostic> diagnostics)
    {
        if (arguments.Positional.Count > position && arguments.Keywords.ContainsKey(keyword))
        {
            AddDiagnostic(diagnostics, "LA3151", $"{OpenSignature} got multiple values for argument '{keyword}'.", arguments.Keywords[keyword].Span);
        }
    }

    private static void AnalyzePathReadTextCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        AnalyzeEncodingArgument(
            arguments,
            0,
            "encoding",
            "LA3049",
            "Path.read_text() only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.",
            "Path.read_text() only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.",
            diagnostics,
            bindings);
        AnalyzeErrorsArgument(
            arguments,
            1,
            "errors",
            "LA3049",
            "Path.read_text() only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.",
            "Path.read_text() only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.",
            diagnostics,
            bindings);
        AnalyzeNewlineArgument(
            arguments,
            2,
            "newline",
            "LA3049",
            "Path.read_text() newline must be None, '', '\\n', '\\r', or '\\r\\n'.",
            "Path.read_text() newline must be None, '', '\\n', '\\r', or '\\r\\n'.",
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
            "Path.write_text() only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.",
            "Path.write_text() only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.",
            diagnostics,
            bindings);
        AnalyzeErrorsArgument(
            arguments,
            2,
            "errors",
            "LA3053",
            "Path.write_text() only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.",
            "Path.write_text() only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.",
            diagnostics,
            bindings);
        AnalyzeNewlineArgument(
            arguments,
            3,
            "newline",
            "LA3054",
            "Path.write_text() newline must be None, '', '\\n', '\\r', or '\\r\\n'.",
            "Path.write_text() newline must be None, '', '\\n', '\\r', or '\\r\\n'.",
            diagnostics,
            bindings);
    }

    private static void AnalyzePathOpenCall(ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        if (arguments.TryGetValue(0, "mode", out var modeExpression))
        {
            if (modeExpression is NoneLiteralExpressionSyntax)
            {
                AddDiagnostic(diagnostics, "LA3060", "Path.open(mode) expects mode to be a string; omit mode to use read mode.", modeExpression.Span);
            }
            else if (!StaticAbstractValueResolver.TryResolveKnownString(modeExpression, bindings, out _))
            {
                if (StaticAbstractValueResolver.IsDefinitelyKnownLiteral(modeExpression, bindings))
                {
                    AddDiagnostic(diagnostics, "LA3060", "Path.open(mode) expects mode to be a string.", modeExpression.Span);
                }
            }
            else if (StaticAbstractValueResolver.TryResolveKnownString(modeExpression, bindings, out var modeText))
            {
                if (modeText.Contains('b', StringComparison.Ordinal))
                {
                    AddDiagnostic(diagnostics, "LA3061", "Path.open() only supports text modes; binary modes like 'rb' and 'wb' are unsupported.", modeExpression.Span);
                }
                else if (modeText.Contains('+', StringComparison.Ordinal))
                {
                    AddDiagnostic(diagnostics, "LA3062", "Path.open() does not support updating text modes such as 'r+'.", modeExpression.Span);
                }
                else if (!IsSupportedTextMode(modeText))
                {
                    AddDiagnostic(diagnostics, "LA3062", "Path.open() only supports modes 'r', 'w', and 'a' with optional text marker 't'.", modeExpression.Span);
                }
            }
        }

        StaticKnownCallArgumentChecks.AnalyzeIntegerOrNoneArgument(arguments, 1, "buffering", "Path.open(..., buffering=...) expects an integer or None.", diagnostics, bindings);
        AnalyzeEncodingArgument(
            arguments,
            2,
            "encoding",
            "LA3063",
            "Path.open() only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.",
            "Path.open() only supports encoding='utf-8', 'utf-8-sig', or 'latin-1'.",
            diagnostics,
            bindings);
        AnalyzeErrorsArgument(
            arguments,
            3,
            "errors",
            "LA3063",
            "Path.open() only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.",
            "Path.open() only supports text error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.",
            diagnostics,
            bindings);
        AnalyzeNewlineArgument(
            arguments,
            4,
            "newline",
            "LA3064",
            "Path.open() newline must be None, '', '\\n', '\\r', or '\\r\\n'.",
            "Path.open() newline must be None, '', '\\n', '\\r', or '\\r\\n'.",
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
        if (arguments.Positional.Count + arguments.Keywords.Count > 1)
        {
            AddDiagnostic(diagnostics, "LA3108", $"file.{memberName}([size]) expects zero or one integer argument.", span);
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
            !encodingText.Equals("utf-8-sig", StringComparison.OrdinalIgnoreCase) &&
            !encodingText.Equals("latin-1", StringComparison.OrdinalIgnoreCase) &&
            !encodingText.Equals("latin1", StringComparison.OrdinalIgnoreCase) &&
            !encodingText.Equals("iso-8859-1", StringComparison.OrdinalIgnoreCase))
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
                 newlineText is not ("" or "\n" or "\r" or "\r\n"))
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
            !StaticTextContractFacts.IsSupportedErrorName(errorsText))
        {
            AddDiagnostic(diagnostics, code, unsupportedErrorsMessage, errorsExpression.Span);
        }
    }

    private static bool IsSupportedTextMode(string mode)
        => mode is "r" or "rt" or "w" or "wt" or "a" or "at";

    private static void AnalyzeOpenerArgument(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(7, "opener", out var openerExpression) ||
            openerExpression is NoneLiteralExpressionSyntax)
        {
            return;
        }

        if (StaticAbstractValueResolver.TryResolve(openerExpression, bindings, out var value) &&
            value.Kind != AbstractValueKind.None)
        {
            AddDiagnostic(diagnostics, "LA3006", "open(..., opener=...) is not supported because file access is host-mediated.", openerExpression.Span);
        }
    }

    private static void AnalyzeCloseFdArgument(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(6, "closefd", out var closeFdExpression) ||
            !StaticAbstractValueResolver.TryResolve(closeFdExpression, bindings, out var value) ||
            value.Kind != AbstractValueKind.Boolean ||
            value.Value is not false)
        {
            return;
        }

        AddDiagnostic(diagnostics, "LA3006", "open(..., closefd=False) is not supported for host-mediated paths.", closeFdExpression.Span);
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
        => AddDiagnostic(diagnostics, code, message, span, null);

    private static void AddDiagnostic(
        List<LythonDiagnostic> diagnostics,
        string code,
        string message,
        LythonSourceSpan span,
        StaticDiagnosticProof? proof)
        => StaticDiagnosticSink.AddError(diagnostics, code, message, span, proof);
}
