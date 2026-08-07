using Lokad.Lython.Runtime;
using static Lokad.Lython.Frontend.StaticKnownCallArgumentChecks;

namespace Lokad.Lython.Frontend;

internal static class StaticProcessContractFamily
{
    private const int ArgsIndex = 0;
    private const int InputIndex = 1;
    private const int CwdIndex = 2;
    private const int TimeoutIndex = 3;
    private const int CheckIndex = 4;
    private const int CaptureOutputIndex = 5;
    private const int StdinIndex = 6;
    private const int StdoutIndex = 7;
    private const int StderrIndex = 8;
    private const int ShellIndex = 9;
    private const int TextIndex = 10;
    private const int EncodingIndex = 11;
    private const int ErrorsIndex = 12;
    private const int EnvIndex = 13;
    private const int UniversalNewlinesIndex = 14;
    private const int PopenBufsizeIndex = 1;
    private const int PopenStdinIndex = 3;
    private const int PopenStdoutIndex = 4;
    private const int PopenStderrIndex = 5;
    private const int PopenCloseFdsIndex = 7;
    private const int PopenShellIndex = 8;
    private const int PopenCwdIndex = 9;
    private const int PopenEnvIndex = 10;
    private const int PopenUniversalNewlinesIndex = 11;
    private const int PopenCreationFlagsIndex = 13;
    private const int PopenRestoreSignalsIndex = 14;
    private const int PopenStartNewSessionIndex = 15;
    private const int PopenEncodingIndex = 20;
    private const int PopenErrorsIndex = 21;
    private const int PopenTextIndex = 22;
    private const int PopenUmaskIndex = 23;
    private const int PopenPipeSizeIndex = 24;

    public static bool AnalyzeKnownCallArgumentTypes(
        string targetName,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (string.Equals(targetName, LythonKnownCallableSignatures.SubprocessCompletedProcess.Name, StringComparison.Ordinal))
        {
            var completedProcessEmitted = AnalyzeIntegerArgument(arguments, 1, "returncode", "subprocess.CompletedProcess(..., returncode=...) expects an integer.", diagnostics, bindings);
            return completedProcessEmitted;
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.SubprocessList2Cmdline.Name, StringComparison.Ordinal))
        {
            return AnalyzeIterableOfStringsArgument(arguments, 0, "seq", "subprocess.list2cmdline(seq) expects an iterable of strings.", diagnostics, bindings, rejectSingleString: true);
        }

        if (string.Equals(targetName, LythonKnownCallableSignatures.SubprocessPopen.Name, StringComparison.Ordinal))
        {
            return AnalyzePopenArguments(arguments, diagnostics, bindings);
        }

        if (!IsSubprocessKnownCall(targetName))
        {
            return false;
        }

        var owner = targetName;
        var emitted = AnalyzeSubprocessArgsArgument(arguments, owner, diagnostics, bindings);
        emitted |= AnalyzeStringOrNoneArgument(arguments, InputIndex, "input", $"{owner}(..., input=...) expects a string or None.", diagnostics, bindings);
        emitted |= AnalyzePathLikeOrNoneArgument(arguments, CwdIndex, "cwd", $"{owner}(..., cwd=...) expects a string, Path, or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, TimeoutIndex, "timeout", $"{owner}(..., timeout=...) expects an integer or None.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, CheckIndex, "check", $"{owner}(..., check=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, CaptureOutputIndex, "capture_output", $"{owner}(..., capture_output=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, StdinIndex, "stdin", $"{owner}(..., stdin=...) expects a subprocess stream constant or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, StdoutIndex, "stdout", $"{owner}(..., stdout=...) expects a subprocess stream constant or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, StderrIndex, "stderr", $"{owner}(..., stderr=...) expects a subprocess stream constant or None.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, ShellIndex, "shell", $"{owner}(..., shell=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, TextIndex, "text", $"{owner}(..., text=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzeStringOrNoneArgument(arguments, EncodingIndex, "encoding", $"{owner}(..., encoding=...) expects a string or None.", diagnostics, bindings);
        emitted |= AnalyzeStringOrNoneArgument(arguments, ErrorsIndex, "errors", $"{owner}(..., errors=...) expects a string or None.", diagnostics, bindings);
        emitted |= AnalyzeUtf8Encoding(arguments, EncodingIndex, "encoding", $"{owner}(...) only supports encoding='utf-8' or 'utf-8-sig'.", diagnostics, bindings);
        emitted |= AnalyzeTextErrors(arguments, ErrorsIndex, "errors", $"{owner}(...) only supports UTF-8 error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, UniversalNewlinesIndex, "universal_newlines", $"{owner}(..., universal_newlines=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzeSubprocessEnvArgument(arguments, owner, diagnostics, bindings);
        return emitted;
    }

    public static bool TryAnalyze(
        CallExpressionSyntax call,
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (call.Target is not MemberExpressionSyntax { Target: IdentifierExpressionSyntax { Name: "subprocess" } } member ||
            !IsSubprocessMemberName(member.MemberName))
        {
            return false;
        }

        AnalyzeSubprocessCall(member.MemberName, arguments, diagnostics, bindings);
        return true;
    }

    private static bool AnalyzeSubprocessArgsArgument(ConcreteCallArguments arguments, string owner, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeSubprocessArgsArgument(arguments, owner, diagnostics, bindings, ShellIndex);

    private static bool AnalyzeSubprocessArgsArgument(
        ConcreteCallArguments arguments,
        string owner,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        int shellIndex)
    {
        if (!arguments.TryGetValue(ArgsIndex, "args", out var argsExpression))
        {
            return false;
        }

        if (IsShellKnownTrue(arguments, bindings, shellIndex))
        {
            var value = StaticAbstractValueResolver.ResolveOrUnknown(argsExpression, bindings);
            if (value.IsStringLike || value.Kind == AbstractValueKind.Path || StaticKnownCallArgumentChecks.IsUnknown(value))
            {
                return false;
            }

            return AnalyzeIterableOfPathLikeArgument(arguments, ArgsIndex, "args", $"{owner}(args) expects a string command or an iterable of strings or Paths.", diagnostics, bindings, rejectSinglePathLike: false, requireNonEmpty: true);
        }

        return AnalyzeIterableOfPathLikeArgument(arguments, ArgsIndex, "args", $"{owner}(args) expects a non-empty iterable of strings or Paths, not a single string.", diagnostics, bindings, rejectSinglePathLike: true, requireNonEmpty: true);
    }

    private static bool AnalyzeSubprocessEnvArgument(ConcreteCallArguments arguments, string owner, List<LythonDiagnostic> diagnostics, AbstractState bindings)
        => AnalyzeSubprocessEnvArgument(arguments, owner, diagnostics, bindings, EnvIndex);

    private static bool AnalyzeSubprocessEnvArgument(
        ConcreteCallArguments arguments,
        string owner,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings,
        int envIndex)
    {
        if (!arguments.TryGetValue(envIndex, "env", out var envExpression) ||
            envExpression is NoneLiteralExpressionSyntax)
        {
            return false;
        }

        var value = StaticAbstractValueResolver.ResolveOrUnknown(envExpression, bindings);
        if (StaticKnownCallArgumentChecks.IsUnknown(value) || value.Kind == AbstractValueKind.Dict)
        {
            return false;
        }

        if (value.IsLiteralLike)
        {
            AddDiagnostic(diagnostics, "LA3158", $"{owner}(..., env=...) expects a dictionary of strings or None.", envExpression.Span);
            return true;
        }

        return false;
    }

    private static void AnalyzeSubprocessCall(string memberName, ConcreteCallArguments arguments, List<LythonDiagnostic> diagnostics, AbstractState bindings)
    {
        var owner = "subprocess." + memberName;
        if (arguments.TryGetValue(ArgsIndex, "args", out var argsExpression))
        {
            if (!IsShellKnownTrue(arguments, bindings) &&
                StaticAbstractValueResolver.TryResolveKnownString(argsExpression, bindings, out _))
            {
                AddDiagnostic(diagnostics, "LA3020", $"{owner}(args) expects an iterable of strings or Paths, not a single string.", argsExpression.Span);
            }
            else if (StaticAbstractValueResolver.TryResolveKnownSequenceItems(argsExpression, bindings, out var sequenceItems))
            {
                StaticContractChecks.AnalyzeIterableOfPathLikeLiteral(
                    sequenceItems,
                    "LA3021",
                    $"{owner}(args) expects an iterable of strings or Paths.",
                    diagnostics,
                    requireNonEmpty: true,
                    emptyCode: "LA3027",
                    emptyMessage: $"{owner}(args) expects at least one command part.",
                    emptySpan: argsExpression.Span);
            }
            else if (StaticAbstractFacts.IsDefinitelyKnownNonIterableLiteral(argsExpression, bindings))
            {
                AddDiagnostic(diagnostics, "LA3021", $"{owner}(args) expects an iterable of strings or Paths.", argsExpression.Span);
            }
        }

        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(
            arguments,
            InputIndex,
            "input",
            "LA3022",
            $"{owner}(..., input=...) expects a string or None.",
            diagnostics,
            bindings);
        AnalyzeKnownPathLikeOrNoneArgument(
            arguments,
            CwdIndex,
            "cwd",
            "LA3023",
            $"{owner}(..., cwd=...) expects a string, Path, or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            TimeoutIndex,
            "timeout",
            "LA3024",
            $"{owner}(..., timeout=...) expects an integer or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            CheckIndex,
            "check",
            "LA3025",
            $"{owner}(..., check=...) expects a bool or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            CaptureOutputIndex,
            "capture_output",
            "LA3026",
            $"{owner}(..., capture_output=...) expects a bool or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            StdinIndex,
            "stdin",
            "LA3028",
            $"{owner}(..., stdin=...) expects a subprocess stream constant or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            StdoutIndex,
            "stdout",
            "LA3028",
            $"{owner}(..., stdout=...) expects a subprocess stream constant or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeOptionalIntegerArgument(
            arguments,
            StderrIndex,
            "stderr",
            "LA3028",
            $"{owner}(..., stderr=...) expects a subprocess stream constant or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            ShellIndex,
            "shell",
            "LA3029",
            $"{owner}(..., shell=...) expects a bool or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            TextIndex,
            "text",
            "LA3030",
            $"{owner}(..., text=...) expects a bool or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(
            arguments,
            EncodingIndex,
            "encoding",
            "LA3031",
            $"{owner}(..., encoding=...) expects a string or None.",
            diagnostics,
            bindings);
        StaticContractChecks.AnalyzeKnownStringOrNoneArgument(
            arguments,
            ErrorsIndex,
            "errors",
            "LA3032",
            $"{owner}(..., errors=...) expects a string or None.",
            diagnostics,
            bindings);
        AnalyzeUtf8Encoding(arguments, EncodingIndex, "encoding", $"{owner}(...) only supports encoding='utf-8' or 'utf-8-sig'.", diagnostics, bindings);
        AnalyzeTextErrors(arguments, ErrorsIndex, "errors", $"{owner}(...) only supports UTF-8 error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.", diagnostics, bindings);
        StaticContractChecks.AnalyzeKnownBooleanOrNoneArgument(
            arguments,
            UniversalNewlinesIndex,
            "universal_newlines",
            "LA3030",
            $"{owner}(..., universal_newlines=...) expects a bool or None.",
            diagnostics,
            bindings);
    }

    private static bool AnalyzeUtf8Encoding(
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
            text.Equals("utf-8-sig", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        StaticDiagnosticSink.AddError(diagnostics, "LA3031", message, expression.Span);
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
            IsSupportedTextError(text))
        {
            return false;
        }

        StaticDiagnosticSink.AddError(diagnostics, "LA3032", message, expression.Span);
        return true;
    }

    private static bool IsSupportedTextError(string errors)
        => errors.Equals("strict", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("ignore", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("replace", StringComparison.OrdinalIgnoreCase) ||
           errors.Equals("backslashreplace", StringComparison.OrdinalIgnoreCase);

    private static bool IsSubprocessKnownCall(string targetName)
        => string.Equals(targetName, LythonKnownCallableSignatures.SubprocessRun.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.SubprocessCall.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.SubprocessCheckCall.Name, StringComparison.Ordinal) ||
           string.Equals(targetName, LythonKnownCallableSignatures.SubprocessCheckOutput.Name, StringComparison.Ordinal);

    private static bool IsSubprocessMemberName(string memberName)
        => memberName is "run" or "call" or "check_call" or "check_output";

    private static bool IsShellKnownTrue(ConcreteCallArguments arguments, AbstractState bindings)
        => IsShellKnownTrue(arguments, bindings, ShellIndex);

    private static bool IsShellKnownTrue(ConcreteCallArguments arguments, AbstractState bindings, int shellIndex)
    {
        if (!arguments.TryGetValue(shellIndex, "shell", out var shellExpression))
        {
            return false;
        }

        return StaticAbstractValueResolver.TryResolve(shellExpression, bindings, out var value) &&
            value.Kind == AbstractValueKind.Boolean &&
            value.Value is true;
    }

    private static bool AnalyzePopenArguments(
        ConcreteCallArguments arguments,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        const string owner = "subprocess.Popen";
        var emitted = AnalyzeSubprocessArgsArgument(arguments, owner, diagnostics, bindings, PopenShellIndex);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, PopenBufsizeIndex, "bufsize", $"{owner}(..., bufsize=...) expects an integer.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, PopenStdinIndex, "stdin", $"{owner}(..., stdin=...) expects a subprocess stream constant or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, PopenStdoutIndex, "stdout", $"{owner}(..., stdout=...) expects a subprocess stream constant or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, PopenStderrIndex, "stderr", $"{owner}(..., stderr=...) expects a subprocess stream constant or None.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, PopenCloseFdsIndex, "close_fds", $"{owner}(..., close_fds=...) expects a bool.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, PopenShellIndex, "shell", $"{owner}(..., shell=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzePathLikeOrNoneArgument(arguments, PopenCwdIndex, "cwd", $"{owner}(..., cwd=...) expects a string, Path, or None.", diagnostics, bindings);
        emitted |= AnalyzeSubprocessEnvArgument(arguments, owner, diagnostics, bindings, PopenEnvIndex);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, PopenUniversalNewlinesIndex, "universal_newlines", $"{owner}(..., universal_newlines=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, PopenCreationFlagsIndex, "creationflags", $"{owner}(..., creationflags=...) expects an integer.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, PopenRestoreSignalsIndex, "restore_signals", $"{owner}(..., restore_signals=...) expects a bool.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, PopenStartNewSessionIndex, "start_new_session", $"{owner}(..., start_new_session=...) expects a bool.", diagnostics, bindings);
        emitted |= AnalyzeStringOrNoneArgument(arguments, PopenEncodingIndex, "encoding", $"{owner}(..., encoding=...) expects a string or None.", diagnostics, bindings);
        emitted |= AnalyzeStringOrNoneArgument(arguments, PopenErrorsIndex, "errors", $"{owner}(..., errors=...) expects a string or None.", diagnostics, bindings);
        emitted |= AnalyzeBooleanOrNoneArgument(arguments, PopenTextIndex, "text", $"{owner}(..., text=...) expects a bool or None.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, PopenUmaskIndex, "umask", $"{owner}(..., umask=...) expects an integer.", diagnostics, bindings);
        emitted |= AnalyzeIntegerOrNoneArgument(arguments, PopenPipeSizeIndex, "pipesize", $"{owner}(..., pipesize=...) expects an integer.", diagnostics, bindings);
        emitted |= AnalyzeUtf8Encoding(arguments, PopenEncodingIndex, "encoding", $"{owner}(...) only supports encoding='utf-8' or 'utf-8-sig'.", diagnostics, bindings);
        emitted |= AnalyzeTextErrors(arguments, PopenErrorsIndex, "errors", $"{owner}(...) only supports UTF-8 error handlers 'strict', 'ignore', 'replace', and 'backslashreplace'.", diagnostics, bindings);
        return emitted;
    }

    private static void AnalyzeKnownPathLikeOrNoneArgument(
        ConcreteCallArguments arguments,
        int position,
        string keyword,
        string code,
        string message,
        List<LythonDiagnostic> diagnostics,
        AbstractState bindings)
    {
        if (!arguments.TryGetValue(position, keyword, out var expression) ||
            expression is NoneLiteralExpressionSyntax)
        {
            return;
        }

        var value = StaticAbstractValueResolver.ResolveOrUnknown(expression, bindings);
        if (value.Kind is AbstractValueKind.Unknown or AbstractValueKind.Never ||
            value.IsStringLike ||
            value.Kind == AbstractValueKind.Path)
        {
            return;
        }

        if (value.IsLiteralLike)
        {
            AddDiagnostic(diagnostics, code, message, expression.Span);
        }
    }

    private static void AddDiagnostic(List<LythonDiagnostic> diagnostics, string code, string message, LythonSourceSpan span)
    {
        StaticDiagnosticSink.AddError(diagnostics, code, message, span);
    }
}
