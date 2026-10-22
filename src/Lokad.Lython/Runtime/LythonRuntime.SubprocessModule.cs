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
                "run" => BuiltinCallable.Create(LythonKnownCallableSignatures.SubprocessRun, Run, RunAsync),
                "call" => BuiltinCallable.Create(LythonKnownCallableSignatures.SubprocessCall, Call, CallAsync),
                "check_call" => BuiltinCallable.Create(LythonKnownCallableSignatures.SubprocessCheckCall, CheckCall, CheckCallAsync),
                "check_output" => BuiltinCallable.Create(LythonKnownCallableSignatures.SubprocessCheckOutput, CheckOutput, CheckOutputAsync),
                "CompletedProcess" => BuiltinCallable.Create(LythonKnownCallableSignatures.SubprocessCompletedProcess, CompletedProcess),
                "CalledProcessError" => SubprocessCalledProcessErrorType.Instance,
                "SubprocessError" => new ExceptionTypeValue(ModuleException("subprocess", "SubprocessError")),
                "TimeoutExpired" => SubprocessTimeoutExpiredType.Instance,
                "Popen" => BuiltinCallable.Create(LythonKnownCallableSignatures.SubprocessPopen, Popen),
                "list2cmdline" => BuiltinCallable.Create(LythonKnownCallableSignatures.SubprocessList2Cmdline, List2Cmdline, List2CmdlineAsync),
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
        context.CheckExecution(span);
        if (arguments.Length < 2)
        {
            throw new LythonRuntimeException("TypeError", "subprocess.CompletedProcess(args, returncode, stdout=None, stderr=None) expects args and returncode.", span);
        }

        var returnCode = arguments[1] switch
        {
            BigInteger integer => integer,
            int integer => new BigInteger(integer),
            bool flag => flag ? BigInteger.One : BigInteger.Zero,
            _ => throw new LythonRuntimeException("TypeError", "subprocess.CompletedProcess(..., returncode=...) expects an integer.", span)
        };

        return new PyCompletedProcess(
            arguments[0],
            returnCode,
            GetArgument(arguments, 2),
            GetArgument(arguments, 3));
    }

    private static object List2Cmdline(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => List2CmdlineCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

    private static ValueTask<object> List2CmdlineAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => List2CmdlineCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> List2CmdlineCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        var first = true;
        try
        {
            if (asynchronous)
                await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false)) Append(item);
            else foreach (var item in ToSequence(arguments[0], span, context)) Append(item);
            context.CheckExecution(span);
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
        void Append(object item)
        {
            context.CheckExecution(span);
            if (!PyStringOps.TryAsString(item, out var text)) throw RuntimeErrors.Type("subprocess.list2cmdline(seq) expects an iterable of strings.", span);
            if (!first) builder.Append((byte)' ');
            first = false;
            AppendWindowsCommandLineArgument(builder, text, context, span);
        }
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
            throw RuntimeErrors.Capability("subprocess is not available in this host.", span);
        }

        using var storage = context.MemoryGovernor.ReserveTemporary(512, span);
        var invocation = BuildSubprocessInvocation(new BoundSubprocessArguments(arguments), span, context, owner, policy, storage);

        context.CheckExecution(span);
        var result = context.RunSubprocess(invocation.Request, span);

        return CompleteSubprocessRun(result, invocation, span, context, storage);
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
            throw RuntimeErrors.Capability("subprocess is not available in this host.", span);
        }

        using var storage = context.MemoryGovernor.ReserveTemporary(512, span);
        var invocation = BuildSubprocessInvocation(new BoundSubprocessArguments(arguments), span, context, owner, policy, storage);

        context.CheckExecution(span);
        var result = await context.RunSubprocessAsync(invocation.Request, span).ConfigureAwait(false);

        return CompleteSubprocessRun(result, invocation, span, context, storage);
    }

    private static SubprocessInvocation BuildSubprocessInvocation(
        BoundSubprocessArguments arguments,
        LythonSourceSpan span,
        ExecutionContext context,
        string owner,
        SubprocessInvocationPolicy policy, MemoryGovernor.TemporaryMemoryReservation storage)
    {
        if (!arguments.HasArgs)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(args, ...) expects at least one argument.", span);
        }

        var shell = ParseSubprocessOptionalBool(arguments.Shell, false, owner, "shell", span);
        var runner = context.Host.SubprocessRunner ??
            throw RuntimeErrors.Capability("subprocess is not available in this host.", span);
        if (shell && !runner.AllowsShellInvocation)
        {
            throw RuntimeErrors.Capability("subprocess shell invocation is not available in this host.", span);
        }

        var (args, argsBytes) = ParseSubprocessArgs(arguments.Args, shell, owner, span, context, storage);
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
        var (environment, envBytes) = ParseSubprocessEnvironment(arguments.Environment, owner, span, context, storage);
        var cwdBytes = checked(256L + 32L * (context.Host.Cwd.Length + (arguments.CurrentDirectory is PyPath path ? path.Value.Utf8Bytes.Length : arguments.CurrentDirectory is PyString text ? text.Utf8Bytes.Length : 0L)));
        storage.Grow(cwdBytes, span);
        var cwd = ParseSubprocessCwd(arguments.CurrentDirectory, owner, span, context);

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
                Timeout: timeout is { } timeoutValue ? TimeSpan.FromMilliseconds(timeoutValue) : null) { MemoryBudget = context.MemoryGovernor.Budget, MemoryGovernor = context.MemoryGovernor },
            check,
            policy.Completion, arguments.Args, checked(512L + argsBytes + envBytes + cwdBytes));
    }

    private static object CompleteSubprocessRun(
        LythonSubprocessResult result,
        SubprocessInvocation invocation,
        LythonSourceSpan span,
        ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation storage)
    {
        if (invocation.CompletionKind == SubprocessCompletionKind.ReturnCode && (!invocation.Check || result.ReturnCode == 0))
            return new BigInteger(result.ReturnCode);
        object stdout = invocation.Request.StandardOutput == LythonSubprocessStreamMode.Pipe
            ? DecodeSubprocessOutput(result.StandardOutputUtf8, invocation.Request.TextEncoding, invocation.Request.TextErrorMode, context, span)
            : PyNone.Instance;
        object stderr = invocation.Request.StandardError == LythonSubprocessStreamMode.Pipe
            ? DecodeSubprocessOutput(result.StandardErrorUtf8, invocation.Request.TextEncoding, invocation.Request.TextErrorMode, context, span)
            : PyNone.Instance;
        var args = invocation.Args;
        context.CheckExecution(span);

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

        if (invocation.CompletionKind == SubprocessCompletionKind.CompletedProcess)
        {
            storage.Commit(128);
            return new PyCompletedProcess(args, new BigInteger(result.ReturnCode), stdout, stderr);
        }
        return invocation.CompletionKind switch
        {
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

        context.CheckExecution(span);
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
        return new LythonRuntimeException(ModuleException("subprocess", "CalledProcessError"), message, span, null, payload);
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

    private static void AppendWindowsCommandLineArgument(GovernedByteBuilder builder, PyString argument,
        ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = argument.Utf8Bytes.Span;
        var quoted = bytes.IsEmpty;
        for (var i = 0; i < bytes.Length && !quoted; i++)
        {
            if ((i & 4095) == 0) context.CheckExecution(span);
            quoted = bytes[i] is (byte)' ' or (byte)'\t';
        }
        if (quoted) builder.Append((byte)'"');
        var slashes = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            if ((i & 4095) == 0) context.CheckExecution(span);
            var ch = bytes[i];
            if (ch == (byte)'\\') { slashes++; continue; }
            AppendSlashes(ch == (byte)'"' ? checked(2L * slashes + 1) : slashes);
            slashes = 0;
            builder.Append(ch);
        }
        AppendSlashes(quoted ? 2L * slashes : slashes);
        if (quoted) builder.Append((byte)'"');
        void AppendSlashes(long count)
        {
            while (count > 0)
            {
                context.CheckExecution(span);
                var chunk = (int)Math.Min(count, 4096);
                builder.AppendRepeated((byte)'\\', chunk);
                count -= chunk;
            }
        }
    }
}
