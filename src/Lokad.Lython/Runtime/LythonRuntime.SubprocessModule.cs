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

        static void AppendWindowsCommandLineArgument(System.Text.StringBuilder builder, string argument)
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
    }


}
