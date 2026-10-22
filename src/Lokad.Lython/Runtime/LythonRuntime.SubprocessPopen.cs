using System.Collections;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly LythonSourceSpan PopenSyntheticSpan = new(0, 0, 1, 1);

    private const int PopenArgsIndex = 0;
    private const int PopenBufsizeIndex = 1;
    private const int PopenExecutableIndex = 2;
    private const int PopenStdinIndex = 3;
    private const int PopenStdoutIndex = 4;
    private const int PopenStderrIndex = 5;
    private const int PopenPreExecIndex = 6;
    private const int PopenCloseFdsIndex = 7;
    private const int PopenShellIndex = 8;
    private const int PopenCwdIndex = 9;
    private const int PopenEnvIndex = 10;
    private const int PopenUniversalNewlinesIndex = 11;
    private const int PopenStartupInfoIndex = 12;
    private const int PopenCreationFlagsIndex = 13;
    private const int PopenRestoreSignalsIndex = 14;
    private const int PopenStartNewSessionIndex = 15;
    private const int PopenPassFdsIndex = 16;
    private const int PopenUserIndex = 17;
    private const int PopenGroupIndex = 18;
    private const int PopenExtraGroupsIndex = 19;
    private const int PopenEncodingIndex = 20;
    private const int PopenErrorsIndex = 21;
    private const int PopenTextIndex = 22;
    private const int PopenUmaskIndex = 23;
    private const int PopenPipeSizeIndex = 24;
    private const int PopenProcessGroupIndex = 25;

    // Wrapper-state slice (MG21): retained Popen handles own the 128B
    // constructed-value unit, and each pipe-stream wrapper they create owns
    // one 64B slot, both for the handle lifetime.
    private const long PopenWrapperBytes = 128;
    private const long PopenStreamBytes = 64;

    private static object Popen(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => PopenCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

    private static ValueTask<object> PopenAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        => PopenCoreAsync(arguments, span, context, true);

    private static async ValueTask<object> PopenCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        static async ValueTask ValidateCompatibilityOptions(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (HasArgument(arguments, PopenBufsizeIndex) &&
                !PyNumberOps.TryAsInteger(GetArgument(arguments, PopenBufsizeIndex), out _))
            {
                throw new LythonRuntimeException("TypeError", "subprocess.Popen(..., bufsize=...) expects an integer.", span);
            }

            RequirePopenNone(arguments, PopenExecutableIndex, "executable", span);
            RequirePopenNone(arguments, PopenPreExecIndex, "preexec_fn", span);
            RequirePopenNone(arguments, PopenStartupInfoIndex, "startupinfo", span);
            RequirePopenIntegerDefault(arguments, PopenCreationFlagsIndex, "creationflags", BigInteger.Zero, span);
            await RequirePopenBooleanDefaultAsync(arguments, PopenCloseFdsIndex, "close_fds", true, span, context, asynchronous).ConfigureAwait(false);
            await RequirePopenBooleanDefaultAsync(arguments, PopenRestoreSignalsIndex, "restore_signals", true, span, context, asynchronous).ConfigureAwait(false);
            await RequirePopenBooleanDefaultAsync(arguments, PopenStartNewSessionIndex, "start_new_session", false, span, context, asynchronous).ConfigureAwait(false);
            await RequirePopenEmptySequenceAsync(arguments, PopenPassFdsIndex, "pass_fds", span, context, asynchronous).ConfigureAwait(false);
            RequirePopenNone(arguments, PopenUserIndex, "user", span);
            RequirePopenNone(arguments, PopenGroupIndex, "group", span);
            RequirePopenNone(arguments, PopenExtraGroupsIndex, "extra_groups", span);
            RequirePopenIntegerDefault(arguments, PopenUmaskIndex, "umask", new BigInteger(-1), span);
            RequirePopenIntegerDefault(arguments, PopenPipeSizeIndex, "pipesize", new BigInteger(-1), span);
            RequirePopenNone(arguments, PopenProcessGroupIndex, "process_group", span);
        }

        if (context.Host.SubprocessRunner is null)
        {
            throw RuntimeErrors.Capability("subprocess is not available in this host.", span);
        }

        await ValidateCompatibilityOptions(arguments, span, context, asynchronous).ConfigureAwait(false);
        var pipelineInput = GetArgument(arguments, PopenStdinIndex) as PopenOutputStream;
        var layout = SubprocessRunArgumentLayout.Standard;
        using var storage = context.MemoryGovernor.ReserveTemporary(512L + 8L * (layout.UniversalNewlines + 1), span);
        var shared = new object[layout.UniversalNewlines + 1];
        Array.Fill(shared, PyNone.Instance);
        shared[layout.Args] = GetArgument(arguments, PopenArgsIndex);
        shared[layout.CurrentDirectory] = GetArgument(arguments, PopenCwdIndex);
        shared[layout.StandardInput] = pipelineInput is null
            ? GetArgument(arguments, PopenStdinIndex)
            : new BigInteger(SubprocessPipe);
        shared[layout.StandardOutput] = GetArgument(arguments, PopenStdoutIndex);
        shared[layout.StandardError] = GetArgument(arguments, PopenStderrIndex);
        shared[layout.Shell] = GetArgument(arguments, PopenShellIndex);
        shared[layout.Text] = GetArgument(arguments, PopenTextIndex);
        shared[layout.Encoding] = GetArgument(arguments, PopenEncodingIndex);
        shared[layout.Errors] = GetArgument(arguments, PopenErrorsIndex);
        shared[layout.Environment] = GetArgument(arguments, PopenEnvIndex);
        shared[layout.UniversalNewlines] = GetArgument(arguments, PopenUniversalNewlinesIndex);

        var invocation = await BuildSubprocessInvocationAsync(
            new BoundSubprocessArguments(shared),
            span,
            context,
            "subprocess.Popen",
            SubprocessInvocationPolicy.Popen, storage, asynchronous).ConfigureAwait(false);
        var wrapperBytes = PopenWrapperBytes + PopenStreamBytes * (
            (invocation.Request.StandardInput == LythonSubprocessStreamMode.Pipe && pipelineInput is null ? 1 : 0)
            + (invocation.Request.StandardOutput == LythonSubprocessStreamMode.Pipe ? 1 : 0)
            + (invocation.Request.StandardError == LythonSubprocessStreamMode.Pipe ? 1 : 0));
        storage.Grow(wrapperBytes, span);
        var handle = new PyPopen(invocation.Request, invocation.Args, pipelineInput, context, span);
        storage.Commit(checked(invocation.StorageBytes + wrapperBytes));
        return handle;
    }

    private static void RequirePopenNone(object[] arguments, int index, string name, LythonSourceSpan span)
    {
        if (HasArgument(arguments, index))
        {
            throw new LythonRuntimeException(
                "NotImplementedError",
                $"subprocess.Popen(..., {name}=...) is unsupported by Lython's contained buffered process facade.",
                span);
        }
    }

    private static async ValueTask RequirePopenBooleanDefaultAsync(
        object[] arguments,
        int index,
        string name,
        bool expected,
        LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        if (!HasArgument(arguments, index))
        {
            return;
        }

        var item = GetArgument(arguments, index);
        var value = asynchronous ? await IsTruthyAsync(item, context, span).ConfigureAwait(false) : IsTruthy(item, context, span);

        if (value != expected)
        {
            throw new LythonRuntimeException(
                "NotImplementedError",
                $"subprocess.Popen(..., {name}={value}) is unsupported by Lython's contained buffered process facade.",
                span);
        }
    }

    private static void RequirePopenIntegerDefault(
        object[] arguments,
        int index,
        string name,
        BigInteger expected,
        LythonSourceSpan span)
    {
        if (!HasArgument(arguments, index))
        {
            return;
        }

        if (!PyNumberOps.TryAsInteger(GetArgument(arguments, index), out var value))
        {
            throw new LythonRuntimeException("TypeError", $"subprocess.Popen(..., {name}=...) expects an integer.", span);
        }

        if (value != expected)
        {
            throw new LythonRuntimeException(
                "NotImplementedError",
                $"subprocess.Popen(..., {name}={value}) is unsupported by Lython's contained buffered process facade.",
                span);
        }
    }

    private static async ValueTask RequirePopenEmptySequenceAsync(object[] arguments, int index, string name,
        LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        if (!HasArgument(arguments, index)) return;
        bool hasElement;
        if (asynchronous)
        {
            await using var enumerator = ToSequenceAsync(GetArgument(arguments, index), span, context).GetAsyncEnumerator();
            hasElement = await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        else
        {
            using var enumerator = ToSequence(GetArgument(arguments, index), span, context).GetEnumerator();
            hasElement = enumerator.MoveNext();
        }
        if (hasElement) throw new LythonRuntimeException("NotImplementedError", $"subprocess.Popen(..., {name}=...) cannot expose file descriptors through Lython.", span);
    }

    private sealed class PyPopen : IPyDynamicAttributes, IPyAsyncContextManager, IPyRenderableValue
    {
        private readonly LythonSubprocessRequest _request;
        private readonly ExecutionContext _context;
        private readonly object _args;
        private readonly PopenInputStream? _stdin;
        private readonly PopenOutputStream? _stdout;
        private readonly PopenOutputStream? _stderr;
        private readonly PopenOutputStream? _pipelineInput;
        private LythonSubprocessResult? _result;
        private object? _decodedStdout;
        private object? _decodedStderr;
        private Exception? _completionFailure;
        private bool _completionStarted;
        private bool _hasCommunicated;
        private object _communicatedStdout = PyNone.Instance;
        private object _communicatedStderr = PyNone.Instance;

        public PyPopen(
            LythonSubprocessRequest request,
            object args,
            PopenOutputStream? pipelineInput,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            _request = request;
            _args = args;
            _context = context;
            _pipelineInput = pipelineInput;
            _stdin = request.StandardInput == LythonSubprocessStreamMode.Pipe && pipelineInput is null
                ? new PopenInputStream(this, context)
                : null;
            _stdout = request.StandardOutput == LythonSubprocessStreamMode.Pipe
                ? new PopenOutputStream(this, isStandardError: false, context)
                : null;
            _stderr = request.StandardError == LythonSubprocessStreamMode.Pipe
                ? new PopenOutputStream(this, isStandardError: true, context)
                : null;
            context.CheckExecution(span);
            pipelineInput?.AttachAsPipelineInput(context, span);
        }

        public bool IsText => _request.ContentMode == LythonSubprocessContentMode.Text;

        public bool IsCompleted => _result is not null;

        public string EncodingName => _request.TextEncoding == LythonSubprocessTextEncoding.Utf8WithSignature
            ? "utf-8-sig"
            : "utf-8";

        public string ErrorsName => TextErrorName(_request.TextErrorMode);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "args" => _args,
                "stdin" => _stdin is null ? PyNone.Instance : _stdin,
                "stdout" => _stdout is null ? PyNone.Instance : _stdout,
                "stderr" => _stderr is null ? PyNone.Instance : _stderr,
                "returncode" => _result is null ? PyNone.Instance : new BigInteger(_result.ReturnCode),
                "encoding" => IsText ? PyString.FromString(EncodingName) : PyNone.Instance,
                "errors" => IsText ? PyString.FromString(ErrorsName) : PyNone.Instance,
                "universal_newlines" => _request.ContentMode == LythonSubprocessContentMode.Text,
                "communicate" => BoundCallable.Create(
                    (arguments, span, _) => Communicate(arguments, span),
                    async (arguments, span, _) => await CommunicateAsync(arguments, span).ConfigureAwait(false),
                    "Popen.communicate",
                    ["input", "timeout"],
                    0),
                "wait" => BoundCallable.Create(
                    (arguments, span, _) => Wait(arguments, span),
                    async (arguments, span, _) => await WaitAsync(arguments, span).ConfigureAwait(false),
                    "Popen.wait",
                    ["timeout"],
                    0),
                "poll" => BoundCallable.Create((arguments, span, _) => Poll(arguments, span), "Popen.poll", []),
                "send_signal" => UnsupportedMethod("Popen.send_signal", "signals and live process ownership"),
                "terminate" => UnsupportedMethod("Popen.terminate", "signals and live process ownership"),
                "kill" => UnsupportedMethod("Popen.kill", "signals and live process ownership"),
                "pid" => throw new LythonRuntimeException(
                    "NotImplementedError",
                    "Popen.pid is unavailable because Lython's buffered facade does not represent a live process.",
                    null),
                "__enter__" => BoundCallable.Create((arguments, span, _) => EnterBound(arguments, span), "Popen.__enter__", []),
                "__exit__" => BoundCallable.Create(
                    (arguments, span, _) => ExitBound(arguments, span),
                    async (arguments, span, _) => await ExitBoundAsync(arguments, span).ConfigureAwait(false),
                    "Popen.__exit__",
                    ["exc_type", "exc_value", "traceback"]),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public object Enter() => this;

        public ValueTask<object> EnterAsync() => ValueTask.FromResult<object>(this);

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            CloseEndpoints();
            _ = Complete(timeoutRequest: null, null);
            return false;
        }

        public async ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            CloseEndpoints();
            _ = await CompleteAsync(timeoutRequest: null, null).ConfigureAwait(false);
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var returnCode = _result is null ? "None" : _result.ReturnCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return PyString.FromString($"<Popen: returncode: {returnCode} args: {PyRendering.ToPythonString(_args, context)}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public object GetOutput(bool standardError, LythonSourceSpan? span)
        {
            _ = Complete(timeoutRequest: null, span);
            return standardError ? _decodedStderr ?? PyString.Empty : _decodedStdout ?? PyString.Empty;
        }

        public async ValueTask<object> GetOutputAsync(bool standardError, LythonSourceSpan? span)
        {
            _ = await CompleteAsync(timeoutRequest: null, span).ConfigureAwait(false);
            return standardError ? _decodedStderr ?? PyString.Empty : _decodedStdout ?? PyString.Empty;
        }

        public ReadOnlyMemory<byte> CompleteForPipeline(
            SubprocessTimeout? timeoutRequest,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            _ = Complete(timeoutRequest, span, completionPath);
            return _result!.StandardOutputUtf8;
        }

        public async ValueTask<ReadOnlyMemory<byte>> CompleteForPipelineAsync(
            SubprocessTimeout? timeoutRequest,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            _ = await CompleteAsync(timeoutRequest, span, completionPath).ConfigureAwait(false);
            return _result!.StandardOutputUtf8;
        }

        private object Communicate(object[] arguments, LythonSourceSpan span)
        {
            var input = GetArgument(arguments, 0);
            AddCommunicateInput(input, span);
            var timeout = ReadPopenTimeout(arguments, 1, "Popen.communicate", span);
            _ = Complete(timeout, span);
            return CommunicationTuple(span);
        }

        private async ValueTask<object> CommunicateAsync(object[] arguments, LythonSourceSpan span)
        {
            var input = GetArgument(arguments, 0);
            AddCommunicateInput(input, span);
            var timeout = ReadPopenTimeout(arguments, 1, "Popen.communicate", span);
            _ = await CompleteAsync(timeout, span).ConfigureAwait(false);
            return CommunicationTuple(span);
        }

        private object Wait(object[] arguments, LythonSourceSpan span)
        {
            var timeout = ReadPopenTimeout(arguments, 0, "Popen.wait", span);
            return new BigInteger(Complete(timeout, span).ReturnCode);
        }

        private async ValueTask<object> WaitAsync(object[] arguments, LythonSourceSpan span)
        {
            var timeout = ReadPopenTimeout(arguments, 0, "Popen.wait", span);
            var result = await CompleteAsync(timeout, span).ConfigureAwait(false);
            return new BigInteger(result.ReturnCode);
        }

        private object Poll(object[] arguments, LythonSourceSpan span)
        {
            RequirePopenNoArguments(arguments, "Popen.poll()", span);
            return _result is null ? PyNone.Instance : new BigInteger(_result.ReturnCode);
        }

        private object CommunicationTuple(LythonSourceSpan span)
        {
            if (!_hasCommunicated)
            {
                try
                {
                    _communicatedStdout = _stdout is null ? PyNone.Instance : _stdout.ReadRemainingAfterCompletion(span);
                    _communicatedStderr = _stderr is null ? PyNone.Instance : _stderr.ReadRemainingAfterCompletion(span);
                    _hasCommunicated = true;
                }
                finally
                {
                    _stdout?.Close();
                    _stderr?.Close();
                }
            }

            return PyTuple.FromOwnedArray([_communicatedStdout, _communicatedStderr], _context.MemoryGovernor, span);
        }

        private void AddCommunicateInput(object input, LythonSourceSpan span)
        {
            if (input is PyNone)
            {
                return;
            }

            if (_completionStarted)
            {
                throw new LythonRuntimeException("ValueError", "Cannot send input after communication has started", span);
            }

            if (_stdin is null)
            {
                throw new LythonRuntimeException("ValueError", "Cannot send input because stdin is not a pipe", span);
            }

            _ = _stdin.Write(input, span);
        }

        private LythonSubprocessResult Complete(SubprocessTimeout? timeoutRequest, LythonSourceSpan? span)
            => Complete(timeoutRequest, span, new HashSet<PyPopen>());

        private LythonSubprocessResult Complete(
            SubprocessTimeout? timeoutRequest,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            if (_result is not null)
            {
                return _result;
            }

            RethrowCompletionFailure();
            if (!completionPath.Add(this))
            {
                throw new LythonRuntimeException("ValueError", "Popen pipeline contains a cycle.", span);
            }

            _completionStarted = true;
            try
            {
                var request = BuildCompletionRequest(timeoutRequest, span, completionPath);
                _context.CheckExecution(span);
                var result = _context.RunSubprocess(request, span);
                DecodeCompletion(result, span ?? PopenSyntheticSpan);
                _result = result;
                return result;
            }
            catch (LythonRuntimeException ex) when (ex.InnerException is TimeoutException)
            {
                var timeout = CreateTimeoutExpired(timeoutRequest, span ?? PopenSyntheticSpan);
                _completionFailure = timeout;
                throw timeout;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _completionFailure = ex;
                throw;
            }
            finally
            {
                completionPath.Remove(this);
            }
        }

        private async ValueTask<LythonSubprocessResult> CompleteAsync(SubprocessTimeout? timeoutRequest, LythonSourceSpan? span)
            => await CompleteAsync(timeoutRequest, span, new HashSet<PyPopen>()).ConfigureAwait(false);

        private async ValueTask<LythonSubprocessResult> CompleteAsync(
            SubprocessTimeout? timeoutRequest,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            if (_result is not null)
            {
                return _result;
            }

            RethrowCompletionFailure();
            if (!completionPath.Add(this))
            {
                throw new LythonRuntimeException("ValueError", "Popen pipeline contains a cycle.", span);
            }

            _completionStarted = true;
            try
            {
                var request = await BuildCompletionRequestAsync(timeoutRequest, span, completionPath).ConfigureAwait(false);
                _context.CheckExecution(span);
                var result = await _context.RunSubprocessAsync(request, span).ConfigureAwait(false);
                DecodeCompletion(result, span ?? PopenSyntheticSpan);
                _result = result;
                return result;
            }
            catch (LythonRuntimeException ex) when (ex.InnerException is TimeoutException)
            {
                var timeout = CreateTimeoutExpired(timeoutRequest, span ?? PopenSyntheticSpan);
                _completionFailure = timeout;
                throw timeout;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _completionFailure = ex;
                throw;
            }
            finally
            {
                completionPath.Remove(this);
            }
        }

        private LythonSubprocessRequest BuildCompletionRequest(
            SubprocessTimeout? timeoutRequest,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            ReadOnlyMemory<byte> input;
            if (_pipelineInput is null)
            {
                input = _stdin?.SnapshotAndClose(span) ?? ReadOnlyMemory<byte>.Empty;
            }
            else
            {
                var pipeline = _pipelineInput.TakePipelinePayload(timeoutRequest, span, completionPath);
                input = pipeline;
            }

            return _request with
            {
                StandardInputUtf8 = input,
                Timeout = timeoutRequest?.Duration,
            };
        }

        private async ValueTask<LythonSubprocessRequest> BuildCompletionRequestAsync(
            SubprocessTimeout? timeoutRequest,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            ReadOnlyMemory<byte> input;
            if (_pipelineInput is null)
            {
                input = _stdin?.SnapshotAndClose(span) ?? ReadOnlyMemory<byte>.Empty;
            }
            else
            {
                var pipeline = await _pipelineInput
                    .TakePipelinePayloadAsync(timeoutRequest, span, completionPath)
                    .ConfigureAwait(false);
                input = pipeline;
            }

            return _request with
            {
                StandardInputUtf8 = input,
                Timeout = timeoutRequest?.Duration,
            };
        }

        private void DecodeCompletion(LythonSubprocessResult result, LythonSourceSpan span)
        {
            if (_stdout is not null)
            {
                _decodedStdout = CaptureSubprocessOutput(result.StandardOutputUtf8, _request, _context, span);
            }

            if (_stderr is not null)
            {
                _decodedStderr = CaptureSubprocessOutput(result.StandardErrorUtf8, _request, _context, span);
            }

            var ownCapturedBytes =
                (_request.StandardOutput == LythonSubprocessStreamMode.Pipe ? (long)result.StandardOutputUtf8.Length : 0L) +
                (_request.StandardError == LythonSubprocessStreamMode.Pipe ? (long)result.StandardErrorUtf8.Length : 0L);
            if (ownCapturedBytes > 0)
            {
                // Raw buffers stay retained in _result alongside the decoded
                // values, so own them too; both live representations stay
                // charged. Runs once per Popen like the assignment below.
                var rawBytes = checked(32L + ownCapturedBytes);
                _context.MemoryGovernor.Reserve(rawBytes, span);
                _context.MemoryGovernor.Commit(rawBytes);
            }

        }

        private LythonRuntimeException CreateTimeoutExpired(SubprocessTimeout? timeoutRequest, LythonSourceSpan span)
            => CreateSubprocessTimeoutExpired(_args, timeoutRequest, _context, span);

        private void RethrowCompletionFailure()
        {
            if (_completionFailure is not null)
            {
                throw _completionFailure;
            }
        }

        private object EnterBound(object[] arguments, LythonSourceSpan span)
        {
            RequirePopenNoArguments(arguments, "Popen.__enter__()", span);
            return this;
        }

        private object ExitBound(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length != 3)
            {
                throw new LythonRuntimeException("TypeError", "Popen.__exit__() expects three arguments.", span);
            }

            return Exit(arguments[0], arguments[1], arguments[2]);
        }

        private async ValueTask<object> ExitBoundAsync(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length != 3)
            {
                throw new LythonRuntimeException("TypeError", "Popen.__exit__() expects three arguments.", span);
            }

            return await ExitAsync(arguments[0], arguments[1], arguments[2]).ConfigureAwait(false);
        }

        private void CloseEndpoints()
        {
            _stdin?.Close();
            _stdout?.Close();
            _stderr?.Close();
        }

        private static SubprocessTimeout? ReadPopenTimeout(object[] arguments, int index, string owner, LythonSourceSpan span)
            => ParseSubprocessTimeout(GetArgument(arguments, index), owner, span);

        private static object UnsupportedMethod(string name, string capability)
            => new UnsupportedSubprocessCallable(
                name,
                $"{name}() is unsupported because Lython's buffered Popen facade has no {capability}.");
    }

}
