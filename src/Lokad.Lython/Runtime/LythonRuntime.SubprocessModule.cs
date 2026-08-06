using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private const int SubprocessPipe = -1;
    private const int SubprocessStdout = -2;
    private const int SubprocessDevNull = -3;

    private const int SubprocessArgsIndex = 0;
    private const int SubprocessInputIndex = 1;
    private const int SubprocessCwdIndex = 2;
    private const int SubprocessTimeoutIndex = 3;
    private const int SubprocessCheckIndex = 4;
    private const int SubprocessCaptureOutputIndex = 5;
    private const int SubprocessStdinIndex = 6;
    private const int SubprocessStdoutIndex = 7;
    private const int SubprocessStderrIndex = 8;
    private const int SubprocessShellIndex = 9;
    private const int SubprocessTextIndex = 10;
    private const int SubprocessEncodingIndex = 11;
    private const int SubprocessErrorsIndex = 12;
    private const int SubprocessEnvIndex = 13;
    private const int SubprocessUniversalNewlinesIndex = 14;

    private sealed class SubprocessModule : PyModule
    {
        public static readonly SubprocessModule Instance = new();

        private SubprocessModule() : base("subprocess")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "run" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessRun, Run, RunAsync),
                "call" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessCall, Call, CallAsync),
                "check_call" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessCheckCall, CheckCall, CheckCallAsync),
                "check_output" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessCheckOutput, CheckOutput, CheckOutputAsync),
                "CompletedProcess" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessCompletedProcess, CompletedProcess),
                "CalledProcessError" => SubprocessCalledProcessErrorType.Instance,
                "SubprocessError" => new ExceptionTypeValue("SubprocessError"),
                "TimeoutExpired" => new UnsupportedSubprocessCallable("subprocess.TimeoutExpired", "subprocess.TimeoutExpired is not supported by Lython; host timeout results are reported as RuntimeError."),
                "Popen" => new UnsupportedSubprocessCallable("subprocess.Popen", "subprocess.Popen is not supported by Lython; use host-mediated subprocess.run()."),
                "list2cmdline" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessList2Cmdline, List2Cmdline),
                "getoutput" => new UnsupportedSubprocessCallable("subprocess.getoutput", "subprocess.getoutput() is not supported by Lython under the no-new-shell-integration subprocess subset."),
                "getstatusoutput" => new UnsupportedSubprocessCallable("subprocess.getstatusoutput", "subprocess.getstatusoutput() is not supported by Lython under the no-new-shell-integration subprocess subset."),
                "PIPE" => new BigInteger(SubprocessPipe),
                "STDOUT" => new BigInteger(SubprocessStdout),
                "DEVNULL" => new BigInteger(SubprocessDevNull),
                _ => null!
            };

            return value is not null;
        }
    }

    private static object Run(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => InvokeSubprocess(arguments, span, context, "subprocess.run", SubprocessCompletionKind.CompletedProcess);

    private static async ValueTask<object> RunAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await InvokeSubprocessAsync(arguments, span, context, "subprocess.run", SubprocessCompletionKind.CompletedProcess).ConfigureAwait(false);

    private static object Call(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => InvokeSubprocess(arguments, span, context, "subprocess.call", SubprocessCompletionKind.ReturnCode, forcedCheck: false);

    private static async ValueTask<object> CallAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await InvokeSubprocessAsync(arguments, span, context, "subprocess.call", SubprocessCompletionKind.ReturnCode, forcedCheck: false).ConfigureAwait(false);

    private static object CheckCall(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => InvokeSubprocess(arguments, span, context, "subprocess.check_call", SubprocessCompletionKind.ReturnCode, forcedCheck: true);

    private static async ValueTask<object> CheckCallAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await InvokeSubprocessAsync(arguments, span, context, "subprocess.check_call", SubprocessCompletionKind.ReturnCode, forcedCheck: true).ConfigureAwait(false);

    private static object CheckOutput(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => InvokeSubprocess(arguments, span, context, "subprocess.check_output", SubprocessCompletionKind.Stdout, forcedCheck: true, forceStdoutPipe: true);

    private static async ValueTask<object> CheckOutputAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await InvokeSubprocessAsync(arguments, span, context, "subprocess.check_output", SubprocessCompletionKind.Stdout, forcedCheck: true, forceStdoutPipe: true).ConfigureAwait(false);

    private static object CompletedProcess(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (arguments.Length < 2)
        {
            throw new LythonRuntimeException("TypeError", "subprocess.CompletedProcess(args, returncode, stdout=None, stderr=None) expects args and returncode.", span);
        }

        var returnCode = arguments[1] switch
        {
            BigInteger integer => integer,
            int integer => new BigInteger(integer),
            _ => throw new LythonRuntimeException("TypeError", "subprocess.CompletedProcess(..., returncode=...) expects an integer.", span)
        };

        return new PyCompletedProcess(
            arguments[0],
            returnCode,
            GetArgument(arguments, 2),
            GetArgument(arguments, 3));
    }

    private static object List2Cmdline(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        context.CheckExecutionBudget(span);
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "subprocess.list2cmdline(seq) expects one iterable of strings.", span);
        }

        var items = new List<string>();
        foreach (var item in ToSequence(arguments[0], span))
        {
            if (!PyStringOps.TryAsString(item, out var text))
            {
                throw new LythonRuntimeException("TypeError", "subprocess.list2cmdline(seq) expects an iterable of strings.", span);
            }

            items.Add(text.AsString());
        }

        return PyString.FromString(RenderWindowsCommandLine(items));
    }

    private static object InvokeSubprocess(
        object[] arguments,
        LythonSourceSpan span,
        ExecutionContext context,
        string owner,
        SubprocessCompletionKind completionKind,
        bool? forcedCheck = null,
        bool forceStdoutPipe = false)
    {
        if (context.Host.SubprocessRunner is null)
        {
            throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
        }

        var invocation = BuildSubprocessInvocation(arguments, span, context, owner, completionKind, forcedCheck, forceStdoutPipe);

        context.RegisterHostCall(span);
        var result = context.RunSubprocess(invocation.Request, span);

        return CompleteSubprocessRun(result, invocation, span, context);
    }

    private static async ValueTask<object> InvokeSubprocessAsync(
        object[] arguments,
        LythonSourceSpan span,
        ExecutionContext context,
        string owner,
        SubprocessCompletionKind completionKind,
        bool? forcedCheck = null,
        bool forceStdoutPipe = false)
    {
        if (context.Host.SubprocessRunner is null)
        {
            throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
        }

        var invocation = BuildSubprocessInvocation(arguments, span, context, owner, completionKind, forcedCheck, forceStdoutPipe);

        context.RegisterHostCall(span);
        var result = await context.RunSubprocessAsync(invocation.Request, span).ConfigureAwait(false);

        return CompleteSubprocessRun(result, invocation, span, context);
    }

    private static SubprocessInvocation BuildSubprocessInvocation(
        object[] arguments,
        LythonSourceSpan span,
        ExecutionContext context,
        string owner,
        SubprocessCompletionKind completionKind,
        bool? forcedCheck,
        bool forceStdoutPipe)
    {
        if (arguments.Length == 0)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(args, ...) expects at least one argument.", span);
        }

        var shell = ParseSubprocessOptionalBool(arguments, SubprocessShellIndex, false, owner, "shell", span);
        var args = ParseSubprocessArgs(GetArgument(arguments, SubprocessArgsIndex), shell, owner, span);
        int? timeout = HasArgument(arguments, SubprocessTimeoutIndex)
            ? ParseOptionalInt(GetArgument(arguments, SubprocessTimeoutIndex), $"{owner}(..., timeout=...)", span)
            : null;
        var check = forcedCheck ?? ParseSubprocessOptionalBool(arguments, SubprocessCheckIndex, false, owner, "check", span);
        var captureOutput = ParseSubprocessOptionalBool(arguments, SubprocessCaptureOutputIndex, false, owner, "capture_output", span);
        var textMode = ParseSubprocessTextMode(arguments, owner, span);
        var encoding = ParseSubprocessEncoding(arguments, owner, span);
        var errors = ParseSubprocessErrors(arguments, owner, span);
        var environment = ParseSubprocessEnvironment(GetArgument(arguments, SubprocessEnvIndex), owner, span);
        var cwd = ParseSubprocessCwd(GetArgument(arguments, SubprocessCwdIndex), owner, span);

        var stdin = ParseSubprocessInputMode(GetArgument(arguments, SubprocessStdinIndex), owner, span);
        var stdout = ParseSubprocessOutputMode(GetArgument(arguments, SubprocessStdoutIndex), owner, "stdout", span);
        var stderr = ParseSubprocessOutputMode(GetArgument(arguments, SubprocessStderrIndex), owner, "stderr", span);
        var standardInput = ReadOnlyMemory<byte>.Empty;

        if (HasArgument(arguments, SubprocessInputIndex))
        {
            if (HasArgument(arguments, SubprocessStdinIndex))
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) cannot combine input=... and stdin=....", span);
            }

            var inputValue = GetArgument(arguments, SubprocessInputIndex);
            if (!PyStringOps.TryAsString(inputValue, out var input))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(..., input=...) expects a string or None.", span);
            }

            standardInput = input.Utf8Bytes;
            stdin = LythonSubprocessStreamMode.Pipe;
        }

        if (captureOutput)
        {
            if (HasArgument(arguments, SubprocessStdoutIndex) || HasArgument(arguments, SubprocessStderrIndex))
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) cannot combine capture_output=True with stdout=... or stderr=....", span);
            }

            stdout = LythonSubprocessStreamMode.Pipe;
            stderr = LythonSubprocessStreamMode.Pipe;
        }

        if (forceStdoutPipe)
        {
            if (HasArgument(arguments, SubprocessStdoutIndex))
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) does not support an explicit stdout=... argument.", span);
            }

            stdout = LythonSubprocessStreamMode.Pipe;
        }

        return new SubprocessInvocation(
            owner,
            new LythonSubprocessRequest(
                Args: args,
                Cwd: cwd,
                Environment: environment,
                StandardInputUtf8: standardInput,
                StandardInput: stdin,
                StandardOutput: stdout,
                StandardError: stderr,
                UseShell: shell,
                TextMode: textMode,
                Encoding: encoding,
                Errors: errors,
                TimeoutMilliseconds: timeout,
                MaxOutputBytes: context.Limits.MaxStringLength),
            check,
            completionKind);
    }

    private static object CompleteSubprocessRun(
        LythonSubprocessResult result,
        SubprocessInvocation invocation,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        object stdout = invocation.Request.StandardOutput == LythonSubprocessStreamMode.Pipe
            ? DecodeSubprocessOutput(result.StandardOutputUtf8, invocation.Request.Encoding, invocation.Request.Errors, context, span)
            : PyNone.Instance;
        object stderr = invocation.Request.StandardError == LythonSubprocessStreamMode.Pipe
            ? DecodeSubprocessOutput(result.StandardErrorUtf8, invocation.Request.Encoding, invocation.Request.Errors, context, span)
            : PyNone.Instance;
        var args = new PyList(invocation.Request.Args.Select<string, object>(PyString.FromString), context.MemoryGovernor, span);
        context.ObserveCollectionCount(args.Count, span);

        var completed = new PyCompletedProcess(args, new BigInteger(result.ReturnCode), stdout, stderr);
        if (invocation.Check && result.ReturnCode != 0)
        {
            throw CreateCalledProcessError(
                new BigInteger(result.ReturnCode),
                args,
                stdout,
                stderr,
                $"{invocation.Owner}(...) failed with return code {result.ReturnCode}.",
                context,
                span);
        }

        return invocation.CompletionKind switch
        {
            SubprocessCompletionKind.CompletedProcess => completed,
            SubprocessCompletionKind.ReturnCode => new BigInteger(result.ReturnCode),
            SubprocessCompletionKind.Stdout => stdout,
            _ => throw new InvalidOperationException($"Unknown subprocess completion kind: {invocation.CompletionKind}")
        };
    }

    private static PyString DecodeSubprocessOutput(
        ReadOnlyMemory<byte> utf8,
        string? encoding,
        string? errors,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var errorMode = errors?.ToLowerInvariant() switch
        {
            "ignore" => TextErrorMode.Ignore,
            "replace" => TextErrorMode.Replace,
            "backslashreplace" => TextErrorMode.BackslashReplace,
            _ => TextErrorMode.Strict
        };
        var text = DecodeUtf8Text(utf8, context, span, errorMode);
        if (string.Equals(encoding, "utf-8-sig", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = text.AsString();
            if (decoded.Length > 0 && decoded[0] == '\uFEFF')
            {
                text = PyString.FromString(decoded[1..], context.MemoryGovernor, span);
            }
        }

        context.ObserveString(text, span);
        return text;
    }

    private static LythonRuntimeException CreateCalledProcessError(
        BigInteger returnCode,
        object command,
        object output,
        object stderr,
        string message,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var payload = CreateCalledProcessErrorPayload(returnCode, command, output, stderr, context, span);
        return new LythonRuntimeException("CalledProcessError", message, span, payload: payload);
    }

    private static PyDict CreateCalledProcessErrorPayload(
        BigInteger returnCode,
        object command,
        object output,
        object stderr,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var payload = new PyDict(context.MemoryGovernor, span);
        payload.SetItem(PyString.FromString("returncode"), returnCode);
        payload.SetItem(PyString.FromString("cmd"), command);
        payload.SetItem(PyString.FromString("output"), output);
        payload.SetItem(PyString.FromString("stdout"), output);
        payload.SetItem(PyString.FromString("stderr"), stderr);
        return payload;
    }

    private static string RenderWindowsCommandLine(IReadOnlyList<string> arguments)
    {
        var builder = new System.Text.StringBuilder();
        for (var index = 0; index < arguments.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            AppendWindowsCommandLineArgument(builder, arguments[index]);
        }

        return builder.ToString();
    }

    private static void AppendWindowsCommandLineArgument(System.Text.StringBuilder builder, string argument)
    {
        var needsQuotes = argument.Length == 0 || argument.Any(static ch => char.IsWhiteSpace(ch) || ch == '"');
        if (!needsQuotes)
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        var backslashes = 0;
        foreach (var ch in argument)
        {
            if (ch == '\\')
            {
                backslashes++;
                continue;
            }

            if (ch == '"')
            {
                builder.Append('\\', backslashes * 2 + 1);
                builder.Append('"');
                backslashes = 0;
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(ch);
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
    }

    private sealed class UnsupportedSubprocessCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue
    {
        private readonly string _message;

        public UnsupportedSubprocessCallable(string name, string message)
        {
            Name = name;
            _message = message;
        }

        public string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            context.CheckExecutionBudget(span);
            throw new LythonRuntimeException("NotImplementedError", _message, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class SubprocessCalledProcessErrorType : ICallable, IPyDynamicAttributes, IPyRenderableValue
    {
        public static readonly SubprocessCalledProcessErrorType Instance = new();

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString("CalledProcessError"),
                "type" => PyString.FromString("CalledProcessError"),
                _ => null!
            };

            return value is not null;
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                new LythonCallableSignature("subprocess.CalledProcessError", ["returncode", "cmd", "output", "stderr"], RequiredCount: 2),
                "Builtin");
            var returnCode = bound[0] switch
            {
                BigInteger integer => integer,
                int integer => new BigInteger(integer),
                _ => throw new LythonRuntimeException("TypeError", "subprocess.CalledProcessError(returncode, cmd, ...) expects integer returncode.", span)
            };
            var output = GetArgument(bound, 2);
            var stderr = GetArgument(bound, 3);
            var payload = CreateCalledProcessErrorPayload(returnCode, bound[1], output, stderr, context, span);
            return new PyException("CalledProcessError", $"Command failed with return code {returnCode}.", payload);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'subprocess.CalledProcessError'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private readonly record struct SubprocessInvocation(
        string Owner,
        LythonSubprocessRequest Request,
        bool Check,
        SubprocessCompletionKind CompletionKind);

    private enum SubprocessCompletionKind
    {
        CompletedProcess,
        ReturnCode,
        Stdout,
    }

    private static IReadOnlyList<string> ParseSubprocessArgs(object value, bool shell, string owner, LythonSourceSpan span)
    {
        if (PyStringOps.TryAsString(value, out var commandText))
        {
            if (shell)
            {
                return [commandText.AsString()];
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(args) expects an iterable of strings, not a single string.", span);
        }

        if (value is PyPath commandPath)
        {
            if (shell)
            {
                return [commandPath.Value.AsString()];
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(args) expects an iterable of strings or Paths, not a single Path.", span);
        }

        var items = new List<string>();
        foreach (var item in ToSequence(value, span))
        {
            if (item is PyPath path)
            {
                items.Add(path.Value.AsString());
                continue;
            }

            if (!PyStringOps.TryAsString(item, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(args) expects an iterable of strings or Paths.", span);
            }

            items.Add(text.AsString());
        }

        if (items.Count == 0)
        {
            throw new LythonRuntimeException("ValueError", $"{owner}(args) expects at least one command part.", span);
        }

        return items;
    }

    private static bool ParseSubprocessTextMode(object[] arguments, string owner, LythonSourceSpan span)
    {
        var text = ParseSubprocessOptionalBool(arguments, SubprocessTextIndex, false, owner, "text", span);
        var universalNewlines = ParseSubprocessOptionalBool(arguments, SubprocessUniversalNewlinesIndex, false, owner, "universal_newlines", span);
        return text || universalNewlines ||
            HasArgument(arguments, SubprocessEncodingIndex) ||
            HasArgument(arguments, SubprocessErrorsIndex);
    }

    private static string? ParseSubprocessEncoding(object[] arguments, string owner, LythonSourceSpan span)
    {
        var value = GetArgument(arguments, SubprocessEncodingIndex);
        if (value is PyNone or null)
        {
            return null;
        }

        var encodingMode = ParseTextEncoding(value, owner, span);
        return encodingMode switch
        {
            TextEncodingMode.Utf8Bom => "utf-8-sig",
            TextEncodingMode.Latin1 => throw new LythonRuntimeException(
                "ValueError",
                $"{owner}(...) only supports encoding='utf-8' or 'utf-8-sig' because the subprocess host boundary is UTF-8-shaped.",
                span),
            _ => "utf-8"
        };
    }

    private static string? ParseSubprocessErrors(object[] arguments, string owner, LythonSourceSpan span)
    {
        var value = GetArgument(arguments, SubprocessErrorsIndex);
        if (value is PyNone or null)
        {
            return null;
        }

        return ParseTextErrors(value, owner, span) switch
        {
            TextErrorMode.Ignore => "ignore",
            TextErrorMode.Replace => "replace",
            TextErrorMode.BackslashReplace => "backslashreplace",
            _ => "strict"
        };
    }

    private static IReadOnlyDictionary<string, string>? ParseSubprocessEnvironment(object value, string owner, LythonSourceSpan span)
    {
        if (value is PyNone or null)
        {
            return null;
        }

        if (value is not PyDict dict)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(..., env=...) expects a dictionary of strings or None.", span);
        }

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in dict)
        {
            if (!PyStringOps.TryAsString(pair.Key, out var key) ||
                !PyStringOps.TryAsString(pair.Value, out var envValue))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(..., env=...) expects string keys and values.", span);
            }

            environment[key.AsString()] = envValue.AsString();
        }

        return environment;
    }

    private static string? ParseSubprocessCwd(object value, string owner, LythonSourceSpan span)
    {
        if (value is PyNone or null)
        {
            return null;
        }

        if (value is PyPath path)
        {
            return path.Value.AsString();
        }

        if (PyStringOps.TryAsString(value, out var text))
        {
            return text.AsString();
        }

        throw new LythonRuntimeException("TypeError", $"{owner}(..., cwd=...) expects a string, Path, or None.", span);
    }

    private static LythonSubprocessStreamMode ParseSubprocessInputMode(object value, string owner, LythonSourceSpan span)
    {
        if (value is PyNone or null)
        {
            return LythonSubprocessStreamMode.Inherit;
        }

        var constant = ParseSubprocessStreamConstant(value, owner, "stdin", span);
        return constant switch
        {
            SubprocessPipe => LythonSubprocessStreamMode.Pipe,
            SubprocessDevNull => LythonSubprocessStreamMode.DevNull,
            SubprocessStdout => throw new LythonRuntimeException("ValueError", $"{owner}(..., stdin=...) does not support subprocess.STDOUT.", span),
            _ => throw new LythonRuntimeException("ValueError", $"{owner}(..., stdin=...) expects subprocess.PIPE, subprocess.DEVNULL, or None.", span)
        };
    }

    private static LythonSubprocessStreamMode ParseSubprocessOutputMode(object value, string owner, string parameterName, LythonSourceSpan span)
    {
        if (value is PyNone or null)
        {
            return LythonSubprocessStreamMode.Inherit;
        }

        var constant = ParseSubprocessStreamConstant(value, owner, parameterName, span);
        return constant switch
        {
            SubprocessPipe => LythonSubprocessStreamMode.Pipe,
            SubprocessDevNull => LythonSubprocessStreamMode.DevNull,
            SubprocessStdout when parameterName == "stderr" => LythonSubprocessStreamMode.StandardOutput,
            SubprocessStdout => throw new LythonRuntimeException("ValueError", $"{owner}(..., stdout=...) does not support subprocess.STDOUT.", span),
            _ => throw new LythonRuntimeException("ValueError", $"{owner}(..., {parameterName}=...) expects subprocess.PIPE, subprocess.DEVNULL, subprocess.STDOUT for stderr, or None.", span)
        };
    }

    private static int ParseSubprocessStreamConstant(object value, string owner, string parameterName, LythonSourceSpan span)
    {
        if (value is int integer)
        {
            return integer;
        }

        if (value is BigInteger bigInteger && bigInteger >= int.MinValue && bigInteger <= int.MaxValue)
        {
            return (int)bigInteger;
        }

        throw new LythonRuntimeException("TypeError", $"{owner}(..., {parameterName}=...) expects a subprocess stream constant or None.", span);
    }

    private static bool ParseSubprocessOptionalBool(object[] arguments, int index, bool defaultValue, string owner, string parameterName, LythonSourceSpan span)
    {
        var value = GetArgument(arguments, index);
        if (value is PyNone or null)
        {
            return defaultValue;
        }

        return value is bool boolValue
            ? boolValue
            : throw new LythonRuntimeException("TypeError", $"{owner}(..., {parameterName}=...) expects a bool or None.", span);
    }

    private static int ParseOptionalInt(object value, string owner, LythonSourceSpan span)
    {
        return value switch
        {
            int integer => integer,
            BigInteger integer when integer >= int.MinValue && integer <= int.MaxValue => (int)integer,
            PyNone => 0,
            _ => throw new LythonRuntimeException("TypeError", $"{owner} expects an integer.", span)
        };
    }

    private static bool HasArgument(object[] arguments, int index)
    {
        if (index >= arguments.Length)
        {
            return false;
        }

        return arguments[index] is not null and not PyNone;
    }

    private static object GetArgument(object[] arguments, int index)
        => index < arguments.Length ? arguments[index] : PyNone.Instance;
}
