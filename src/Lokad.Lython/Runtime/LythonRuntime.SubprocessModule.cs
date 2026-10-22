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
                "Popen" => BuiltinCallable.Create(LythonKnownCallableSignatures.SubprocessPopen, Popen, PopenAsync),
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

    private static object InvokeSubprocess(object[] arguments, LythonSourceSpan span, ExecutionContext context,
        string owner, SubprocessInvocationPolicy policy)
        => InvokeSubprocessCoreAsync(arguments, span, context, owner, policy, false).GetAwaiter().GetResult();

    private static ValueTask<object> InvokeSubprocessAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context,
        string owner, SubprocessInvocationPolicy policy)
        => InvokeSubprocessCoreAsync(arguments, span, context, owner, policy, true);

    private static async ValueTask<object> InvokeSubprocessCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context,
        string owner, SubprocessInvocationPolicy policy, bool asynchronous)
    {
        if (context.Host.SubprocessRunner is null) throw RuntimeErrors.Capability("subprocess is not available in this host.", span);
        using var storage = context.MemoryGovernor.ReserveTemporary(512, span);
        var invocation = await BuildSubprocessInvocationAsync(new BoundSubprocessArguments(arguments), span, context, owner, policy, storage, asynchronous).ConfigureAwait(false);
        LythonSubprocessResult result;
        try
        {
            context.CheckExecution(span);
            result = asynchronous ? await context.RunSubprocessAsync(invocation.Request, span).ConfigureAwait(false) : context.RunSubprocess(invocation.Request, span);
        }
        catch (LythonRuntimeException ex) when (ex.InnerException is TimeoutException)
        { throw CreateSubprocessTimeoutExpired(invocation.Args, invocation.Timeout, context, span); }
        return await CompleteSubprocessRunAsync(result, invocation, span, context, storage, asynchronous).ConfigureAwait(false);
    }

    private static async ValueTask<SubprocessInvocation> BuildSubprocessInvocationAsync(
        BoundSubprocessArguments arguments,
        LythonSourceSpan span,
        ExecutionContext context,
        string owner,
        SubprocessInvocationPolicy policy, MemoryGovernor.TemporaryMemoryReservation storage, bool asynchronous)
    {
        if (!arguments.HasArgs)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(args, ...) expects at least one argument.", span);
        }

        var captureOutput = asynchronous ? await IsTruthyAsync(arguments.CaptureOutput, context, span).ConfigureAwait(false) : IsTruthy(arguments.CaptureOutput, context, span);
        if (captureOutput && (arguments.HasStandardOutput || arguments.HasStandardError))
            throw new LythonRuntimeException("ValueError", $"{owner}(...) cannot combine capture_output with stdout or stderr.", span);
        if (arguments.HasInput && arguments.HasStandardInput)
            throw new LythonRuntimeException("ValueError", $"{owner}(...) cannot combine input and stdin.", span);
        var shell = asynchronous ? await IsTruthyAsync(arguments.Shell, context, span).ConfigureAwait(false) : IsTruthy(arguments.Shell, context, span);
        var runner = context.Host.SubprocessRunner ??
            throw RuntimeErrors.Capability("subprocess is not available in this host.", span);
        if (shell && !runner.AllowsShellInvocation)
        {
            throw RuntimeErrors.Capability("subprocess shell invocation is not available in this host.", span);
        }

        var textMode = await ParseSubprocessTextModeAsync(arguments, span, context, asynchronous).ConfigureAwait(false);
        var (args, argsBytes) = await ParseSubprocessArgsAsync(arguments.Args, shell, owner, span, context, storage, asynchronous).ConfigureAwait(false);
        var timeout = ParseSubprocessTimeout(arguments.Timeout, owner, span);
        object check = policy.Check switch
        {
            SubprocessCheckPolicy.FromArguments => arguments.Check,
            SubprocessCheckPolicy.Disabled => false,
            SubprocessCheckPolicy.Enabled => true,
            _ => throw new InvalidOperationException($"Unknown subprocess check policy: {policy.Check}"),
        };
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
            standardInput = textMode && arguments.Input is PyString input ? input.Utf8Bytes
                : !textMode && arguments.Input is PyBytes bytes ? bytes.Memory
                : throw RuntimeErrors.Type($"{owner}(..., input=...) expects {(textMode ? "str" : "bytes")} in this content mode.", span);
            if (textMode && encoding == LythonSubprocessTextEncoding.Utf8WithSignature)
            {
                storage.Grow(checked(35L + standardInput.Length), span);
                var encoded = new byte[checked(3 + standardInput.Length)];
                encoded[0] = 0xEF; encoded[1] = 0xBB; encoded[2] = 0xBF;
                for (var offset = 0; offset < standardInput.Length; offset += Math.Min(4096, standardInput.Length - offset))
                {
                    context.CheckExecution(span);
                    standardInput.Span.Slice(offset, Math.Min(4096, standardInput.Length - offset)).CopyTo(encoded.AsSpan(3 + offset));
                }
                standardInput = encoded;
            }
            stdin = LythonSubprocessStreamMode.Pipe;
        }

        if (captureOutput)
        {
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
                Timeout: timeout?.Duration) { MemoryBudget = context.MemoryGovernor.Budget, MemoryGovernor = context.MemoryGovernor },
            check,
            policy.Completion, arguments.Args, checked(512L + argsBytes + envBytes + cwdBytes), timeout);
    }

    private static async ValueTask<object> CompleteSubprocessRunAsync(
        LythonSubprocessResult result,
        SubprocessInvocation invocation,
        LythonSourceSpan span,
        ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation storage, bool asynchronous)
    {
        if (invocation.CompletionKind == SubprocessCompletionKind.ReturnCode && (invocation.Check is false || result.ReturnCode == 0))
            return new BigInteger(result.ReturnCode);
        object stdout = invocation.Request.StandardOutput == LythonSubprocessStreamMode.Pipe
            ? CaptureSubprocessOutput(result.StandardOutputUtf8, invocation.Request, context, span)
            : PyNone.Instance;
        object stderr = invocation.Request.StandardError == LythonSubprocessStreamMode.Pipe
            ? CaptureSubprocessOutput(result.StandardErrorUtf8, invocation.Request, context, span)
            : PyNone.Instance;
        var args = invocation.Args;
        context.CheckExecution(span);

        var check = asynchronous ? await IsTruthyAsync(invocation.Check, context, span).ConfigureAwait(false) : IsTruthy(invocation.Check, context, span);
        if (check && result.ReturnCode != 0)
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

    private static object CaptureSubprocessOutput(ReadOnlyMemory<byte> content, LythonSubprocessRequest request,
        ExecutionContext context, LythonSourceSpan span)
        => request.ContentMode == LythonSubprocessContentMode.Text
            ? DecodeSubprocessOutput(content, request.TextEncoding, request.TextErrorMode, context, span)
            : CopySubprocessBytes(content, context, span);

    private static PyBytes CopySubprocessBytes(ReadOnlyMemory<byte> content, ExecutionContext context, LythonSourceSpan span)
    {
        using var storage = context.MemoryGovernor.ReserveTemporary(PyBytes.EstimateApproximateBytes(content.Length), span);
        context.CheckExecution(span);
        var buffer = new byte[content.Length];
        for (var offset = 0; offset < buffer.Length; offset += Math.Min(4096, buffer.Length - offset))
        {
            context.CheckExecution(span);
            content.Span.Slice(offset, Math.Min(4096, buffer.Length - offset)).CopyTo(buffer.AsSpan(offset));
        }
        context.CheckExecution(span);
        return new PyBytes(buffer, context.MemoryGovernor, span, storage);
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
        if (encoding == LythonSubprocessTextEncoding.Utf8WithSignature && utf8.Span.StartsWith("\uFEFF"u8))
            utf8 = utf8[3..];
        return DecodeUtf8Text(utf8, context, span, errorMode);
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
