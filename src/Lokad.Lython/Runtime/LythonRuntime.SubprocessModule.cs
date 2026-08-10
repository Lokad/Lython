using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private const int SubprocessPipe = -1;
    private const int SubprocessStdout = -2;
    private const int SubprocessDevNull = -3;

    private sealed class SubprocessModule : PyModule
    {
        public static readonly SubprocessModule Instance = new();

        private SubprocessModule() : base("subprocess")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
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
                "TimeoutExpired" => SubprocessTimeoutExpiredType.Instance,
                "Popen" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessPopen, Popen),
                "list2cmdline" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessList2Cmdline, List2Cmdline),
                "getoutput" => new UnsupportedSubprocessCallable("subprocess.getoutput", "subprocess.getoutput() is not supported by Lython under the no-new-shell-integration subprocess subset."),
                "getstatusoutput" => new UnsupportedSubprocessCallable("subprocess.getstatusoutput", "subprocess.getstatusoutput() is not supported by Lython under the no-new-shell-integration subprocess subset."),
                "PIPE" => new BigInteger(SubprocessPipe),
                "STDOUT" => new BigInteger(SubprocessStdout),
                "DEVNULL" => new BigInteger(SubprocessDevNull),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
    }

    private static object Run(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => InvokeSubprocess(arguments, span, context, "subprocess.run", SubprocessInvocationPolicy.Run);

    private static async ValueTask<object> RunAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await InvokeSubprocessAsync(arguments, span, context, "subprocess.run", SubprocessInvocationPolicy.Run).ConfigureAwait(false);

    private static object Call(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => InvokeSubprocess(arguments, span, context, "subprocess.call", SubprocessInvocationPolicy.Call);

    private static async ValueTask<object> CallAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await InvokeSubprocessAsync(arguments, span, context, "subprocess.call", SubprocessInvocationPolicy.Call).ConfigureAwait(false);

    private static object CheckCall(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => InvokeSubprocess(arguments, span, context, "subprocess.check_call", SubprocessInvocationPolicy.CheckCall);

    private static async ValueTask<object> CheckCallAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await InvokeSubprocessAsync(arguments, span, context, "subprocess.check_call", SubprocessInvocationPolicy.CheckCall).ConfigureAwait(false);

    private static object CheckOutput(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => InvokeSubprocess(arguments, span, context, "subprocess.check_output", SubprocessInvocationPolicy.CheckOutput);

    private static async ValueTask<object> CheckOutputAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => await InvokeSubprocessAsync(arguments, span, context, "subprocess.check_output", SubprocessInvocationPolicy.CheckOutput).ConfigureAwait(false);

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
        SubprocessInvocationPolicy policy)
    {
        if (context.Host.SubprocessRunner is null)
        {
            throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
        }

        var invocation = BuildSubprocessInvocation(new BoundSubprocessArguments(arguments), span, context, owner, policy);

        context.RegisterHostCall(span);
        var result = context.RunSubprocess(invocation.Request, span);

        return CompleteSubprocessRun(result, invocation, span, context);
    }

    private static async ValueTask<object> InvokeSubprocessAsync(
        object[] arguments,
        LythonSourceSpan span,
        ExecutionContext context,
        string owner,
        SubprocessInvocationPolicy policy)
    {
        if (context.Host.SubprocessRunner is null)
        {
            throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
        }

        var invocation = BuildSubprocessInvocation(new BoundSubprocessArguments(arguments), span, context, owner, policy);

        context.RegisterHostCall(span);
        var result = await context.RunSubprocessAsync(invocation.Request, span).ConfigureAwait(false);

        return CompleteSubprocessRun(result, invocation, span, context);
    }

    private static SubprocessInvocation BuildSubprocessInvocation(
        BoundSubprocessArguments arguments,
        LythonSourceSpan span,
        ExecutionContext context,
        string owner,
        SubprocessInvocationPolicy policy)
    {
        if (!arguments.HasArgs)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(args, ...) expects at least one argument.", span);
        }

        var shell = ParseSubprocessOptionalBool(arguments.Shell, false, owner, "shell", span);
        var args = ParseSubprocessArgs(arguments.Args, shell, owner, span);
        int? timeout = arguments.HasTimeout
            ? ParseOptionalInt(arguments.Timeout, $"{owner}(..., timeout=...)", span)
            : null;
        var check = policy.Check switch
        {
            SubprocessCheckPolicy.FromArguments => ParseSubprocessOptionalBool(arguments.Check, false, owner, "check", span),
            SubprocessCheckPolicy.Disabled => false,
            SubprocessCheckPolicy.Enabled => true,
            _ => throw new InvalidOperationException($"Unknown subprocess check policy: {policy.Check}"),
        };
        var captureOutput = ParseSubprocessOptionalBool(arguments.CaptureOutput, false, owner, "capture_output", span);
        var textMode = ParseSubprocessTextMode(arguments, owner, span);
        var encoding = ParseSubprocessEncoding(arguments, owner, span);
        var errors = ParseSubprocessErrors(arguments, owner, span);
        var environment = ParseSubprocessEnvironment(arguments.Environment, owner, span);
        var cwd = ParseSubprocessCwd(arguments.CurrentDirectory, owner, span);

        var stdin = ParseSubprocessInputMode(arguments.StandardInput, owner, span);
        var stdout = ParseSubprocessOutputMode(arguments.StandardOutput, owner, "stdout", span);
        var stderr = ParseSubprocessOutputMode(arguments.StandardError, owner, "stderr", span);
        var standardInput = ReadOnlyMemory<byte>.Empty;

        if (arguments.HasInput)
        {
            if (arguments.HasStandardInput)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) cannot combine input=... and stdin=....", span);
            }

            if (!PyStringOps.TryAsString(arguments.Input, out var input))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(..., input=...) expects a string or None.", span);
            }

            standardInput = input.Utf8Bytes;
            stdin = LythonSubprocessStreamMode.Pipe;
        }

        if (captureOutput)
        {
            if (arguments.HasStandardOutput || arguments.HasStandardError)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(...) cannot combine capture_output=True with stdout=... or stderr=....", span);
            }

            stdout = LythonSubprocessStreamMode.Pipe;
            stderr = LythonSubprocessStreamMode.Pipe;
        }

        if (policy.StandardOutput == SubprocessStandardOutputPolicy.ForcePipe)
        {
            if (arguments.HasStandardOutput)
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
                InvocationMode: shell ? LythonSubprocessInvocationMode.Shell : LythonSubprocessInvocationMode.Direct,
                ContentMode: textMode ? LythonSubprocessContentMode.Text : LythonSubprocessContentMode.Binary,
                TextEncoding: encoding,
                TextErrorMode: errors,
                Timeout: timeout is { } timeoutValue ? TimeSpan.FromMilliseconds(timeoutValue) : null,
                OutputLimit: context.Limits.MaxStringLength is { } maximumOutputBytes
                    ? new LythonSubprocessOutputLimit(maximumOutputBytes)
                    : null),
            check,
            policy.Completion);
    }

    private static object CompleteSubprocessRun(
        LythonSubprocessResult result,
        SubprocessInvocation invocation,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        object stdout = invocation.Request.StandardOutput == LythonSubprocessStreamMode.Pipe
            ? DecodeSubprocessOutput(result.StandardOutputUtf8, invocation.Request.TextEncoding, invocation.Request.TextErrorMode, context, span)
            : PyNone.Instance;
        object stderr = invocation.Request.StandardError == LythonSubprocessStreamMode.Pipe
            ? DecodeSubprocessOutput(result.StandardErrorUtf8, invocation.Request.TextEncoding, invocation.Request.TextErrorMode, context, span)
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
        LythonSubprocessTextEncoding encoding,
        LythonSubprocessTextErrorMode errors,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var errorMode = errors switch
        {
            LythonSubprocessTextErrorMode.Ignore => TextErrorMode.Ignore,
            LythonSubprocessTextErrorMode.Replace => TextErrorMode.Replace,
            LythonSubprocessTextErrorMode.BackslashReplace => TextErrorMode.BackslashReplace,
            _ => TextErrorMode.Strict
        };
        var text = DecodeUtf8Text(utf8, context, span, errorMode);
        if (encoding == LythonSubprocessTextEncoding.Utf8WithSignature)
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
        return new LythonRuntimeException("CalledProcessError", message, span, innerException: null, payload: payload);
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

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString("CalledProcessError"),
                "type" => PyString.FromString("CalledProcessError"),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                new LythonCallableSignature("subprocess.CalledProcessError", ["returncode", "cmd", "output", "stderr"], RequiredCount: 2),
                PythonCallableKind.Builtin);
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

    private sealed class SubprocessTimeoutExpiredType : ICallable, IPyDynamicAttributes, IPyRenderableValue
    {
        public static readonly SubprocessTimeoutExpiredType Instance = new();

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString("TimeoutExpired"),
                "type" => PyString.FromString("TimeoutExpired"),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = CallBinder.BindNamedArguments(
                arguments,
                span,
                LythonKnownCallableSignatures.SubprocessTimeoutExpired,
                PythonCallableKind.Builtin);
            return new PyException(
                "TimeoutExpired",
                $"Command timed out after {bound[1]}.",
                CreateTimeoutExpiredPayload(bound[0], bound[1], GetArgument(bound, 2), GetArgument(bound, 3), context, span));
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'subprocess.TimeoutExpired'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private static PyDict CreateTimeoutExpiredPayload(
        object command,
        object timeout,
        object output,
        object stderr,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var payload = new PyDict(context.MemoryGovernor, span);
        payload.SetItem(PyString.FromString("cmd"), command);
        payload.SetItem(PyString.FromString("timeout"), timeout);
        payload.SetItem(PyString.FromString("output"), output);
        payload.SetItem(PyString.FromString("stdout"), output);
        payload.SetItem(PyString.FromString("stderr"), stderr);
        return payload;
    }

    private readonly record struct BoundSubprocessArguments(object[] Values)
    {
        private static readonly SubprocessRunArgumentLayout Layout = SubprocessRunArgumentLayout.Standard;

        public bool HasArgs => Values.Length > Layout.Args;
        public object Args => GetArgument(Values, Layout.Args);
        public bool HasInput => HasArgument(Values, Layout.Input);
        public object Input => GetArgument(Values, Layout.Input);
        public object CurrentDirectory => GetArgument(Values, Layout.CurrentDirectory);
        public bool HasTimeout => HasArgument(Values, Layout.Timeout);
        public object Timeout => GetArgument(Values, Layout.Timeout);
        public object Check => GetArgument(Values, Layout.Check);
        public object CaptureOutput => GetArgument(Values, Layout.CaptureOutput);
        public bool HasStandardInput => HasArgument(Values, Layout.StandardInput);
        public object StandardInput => GetArgument(Values, Layout.StandardInput);
        public bool HasStandardOutput => HasArgument(Values, Layout.StandardOutput);
        public object StandardOutput => GetArgument(Values, Layout.StandardOutput);
        public bool HasStandardError => HasArgument(Values, Layout.StandardError);
        public object StandardError => GetArgument(Values, Layout.StandardError);
        public object Shell => GetArgument(Values, Layout.Shell);
        public object Text => GetArgument(Values, Layout.Text);
        public bool HasEncoding => HasArgument(Values, Layout.Encoding);
        public object Encoding => GetArgument(Values, Layout.Encoding);
        public bool HasErrors => HasArgument(Values, Layout.Errors);
        public object Errors => GetArgument(Values, Layout.Errors);
        public object Environment => GetArgument(Values, Layout.Environment);
        public object UniversalNewlines => GetArgument(Values, Layout.UniversalNewlines);
    }

    private readonly record struct SubprocessInvocation(
        string Owner,
        LythonSubprocessRequest Request,
        bool Check,
        SubprocessCompletionKind CompletionKind);

    private readonly record struct SubprocessInvocationPolicy(
        SubprocessCompletionKind Completion,
        SubprocessCheckPolicy Check,
        SubprocessStandardOutputPolicy StandardOutput)
    {
        public static readonly SubprocessInvocationPolicy Run = new(
            SubprocessCompletionKind.CompletedProcess,
            SubprocessCheckPolicy.FromArguments,
            SubprocessStandardOutputPolicy.FromArguments);

        public static readonly SubprocessInvocationPolicy Call = new(
            SubprocessCompletionKind.ReturnCode,
            SubprocessCheckPolicy.Disabled,
            SubprocessStandardOutputPolicy.FromArguments);

        public static readonly SubprocessInvocationPolicy CheckCall = new(
            SubprocessCompletionKind.ReturnCode,
            SubprocessCheckPolicy.Enabled,
            SubprocessStandardOutputPolicy.FromArguments);

        public static readonly SubprocessInvocationPolicy CheckOutput = new(
            SubprocessCompletionKind.Stdout,
            SubprocessCheckPolicy.Enabled,
            SubprocessStandardOutputPolicy.ForcePipe);

        public static readonly SubprocessInvocationPolicy Popen = new(
            SubprocessCompletionKind.CompletedProcess,
            SubprocessCheckPolicy.Disabled,
            SubprocessStandardOutputPolicy.FromArguments);
    }

    private enum SubprocessCompletionKind
    {
        CompletedProcess,
        ReturnCode,
        Stdout,
    }

    private enum SubprocessCheckPolicy
    {
        FromArguments,
        Disabled,
        Enabled,
    }

    private enum SubprocessStandardOutputPolicy
    {
        FromArguments,
        ForcePipe,
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

    private static bool ParseSubprocessTextMode(BoundSubprocessArguments arguments, string owner, LythonSourceSpan span)
    {
        var text = ParseSubprocessOptionalBool(arguments.Text, false, owner, "text", span);
        var universalNewlines = ParseSubprocessOptionalBool(arguments.UniversalNewlines, false, owner, "universal_newlines", span);
        return text || universalNewlines ||
            arguments.HasEncoding ||
            arguments.HasErrors;
    }

    private static LythonSubprocessTextEncoding ParseSubprocessEncoding(BoundSubprocessArguments arguments, string owner, LythonSourceSpan span)
    {
        var value = arguments.Encoding;
        if (value is PyNone or null)
        {
            return LythonSubprocessTextEncoding.Utf8;
        }

        var encodingMode = ParseTextEncoding(value, owner, span);
        return encodingMode switch
        {
            TextEncodingMode.Utf8Bom => LythonSubprocessTextEncoding.Utf8WithSignature,
            TextEncodingMode.Latin1 => throw new LythonRuntimeException(
                "ValueError",
                $"{owner}(...) only supports encoding='utf-8' or 'utf-8-sig' because the subprocess host boundary is UTF-8-shaped.",
                span),
            _ => LythonSubprocessTextEncoding.Utf8
        };
    }

    private static LythonSubprocessTextErrorMode ParseSubprocessErrors(BoundSubprocessArguments arguments, string owner, LythonSourceSpan span)
    {
        var value = arguments.Errors;
        if (value is PyNone or null)
        {
            return LythonSubprocessTextErrorMode.Strict;
        }

        return ParseTextErrors(value, owner, span) switch
        {
            TextErrorMode.Ignore => LythonSubprocessTextErrorMode.Ignore,
            TextErrorMode.Replace => LythonSubprocessTextErrorMode.Replace,
            TextErrorMode.BackslashReplace => LythonSubprocessTextErrorMode.BackslashReplace,
            _ => LythonSubprocessTextErrorMode.Strict
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

    private static bool ParseSubprocessOptionalBool(object value, bool defaultValue, string owner, string parameterName, LythonSourceSpan span)
    {
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
