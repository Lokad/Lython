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

    private static object Popen(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        static void ValidateCompatibilityOptions(object[] arguments, LythonSourceSpan span)
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
            RequirePopenBooleanDefault(arguments, PopenCloseFdsIndex, "close_fds", expected: true, span);
            RequirePopenBooleanDefault(arguments, PopenRestoreSignalsIndex, "restore_signals", expected: true, span);
            RequirePopenBooleanDefault(arguments, PopenStartNewSessionIndex, "start_new_session", expected: false, span);
            RequirePopenEmptySequence(arguments, PopenPassFdsIndex, "pass_fds", span);
            RequirePopenNone(arguments, PopenUserIndex, "user", span);
            RequirePopenNone(arguments, PopenGroupIndex, "group", span);
            RequirePopenNone(arguments, PopenExtraGroupsIndex, "extra_groups", span);
            RequirePopenIntegerDefault(arguments, PopenUmaskIndex, "umask", new BigInteger(-1), span);
            RequirePopenIntegerDefault(arguments, PopenPipeSizeIndex, "pipesize", new BigInteger(-1), span);
            RequirePopenNone(arguments, PopenProcessGroupIndex, "process_group", span);
        }

        if (context.Host.SubprocessRunner is null)
        {
            throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
        }

        ValidateCompatibilityOptions(arguments, span);
        var pipelineInput = GetArgument(arguments, PopenStdinIndex) as PopenOutputStream;
        var layout = SubprocessRunArgumentLayout.Standard;
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

        var invocation = BuildSubprocessInvocation(
            new BoundSubprocessArguments(shared),
            span,
            context,
            "subprocess.Popen",
            SubprocessInvocationPolicy.Popen);
        var args = new PyList(
            invocation.Request.Args.Select<string, object>(PyString.FromString),
            context.MemoryGovernor,
            span);
        context.ObserveCollectionCount(args.Count, span);
        return new PyPopen(invocation.Request, args, pipelineInput, context, span);
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

    private static void RequirePopenBooleanDefault(
        object[] arguments,
        int index,
        string name,
        bool expected,
        LythonSourceSpan span)
    {
        if (!HasArgument(arguments, index))
        {
            return;
        }

        if (GetArgument(arguments, index) is not bool value)
        {
            throw new LythonRuntimeException("TypeError", $"subprocess.Popen(..., {name}=...) expects a bool.", span);
        }

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

    private static void RequirePopenEmptySequence(object[] arguments, int index, string name, LythonSourceSpan span)
    {
        if (!HasArgument(arguments, index))
        {
            return;
        }

        object[] items;
        try
        {
            items = ToSequence(GetArgument(arguments, index), span).ToArray();
        }
        catch (LythonRuntimeException)
        {
            throw new LythonRuntimeException("TypeError", $"subprocess.Popen(..., {name}=...) expects an iterable.", span);
        }

        if (items.Length != 0)
        {
            throw new LythonRuntimeException(
                "NotImplementedError",
                $"subprocess.Popen(..., {name}=...) cannot expose file descriptors through Lython.",
                span);
        }
    }

    private sealed class PyPopen : IPyDynamicAttributes, IPyAsyncContextManager, IPyRenderableValue
    {
        private readonly LythonSubprocessRequest _request;
        private readonly ExecutionContext _context;
        private readonly PyList _args;
        private readonly PopenInputStream? _stdin;
        private readonly PopenOutputStream? _stdout;
        private readonly PopenOutputStream? _stderr;
        private readonly PopenOutputStream? _pipelineInput;
        private LythonSubprocessResult? _result;
        private PyString? _decodedStdout;
        private PyString? _decodedStderr;
        private Exception? _completionFailure;
        private bool _completionStarted;
        private long _cumulativePipelineOutputBytes;
        private bool _hasCommunicated;
        private object _communicatedStdout = PyNone.Instance;
        private object _communicatedStderr = PyNone.Instance;

        public PyPopen(
            LythonSubprocessRequest request,
            PyList args,
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
            context.ObserveCollectionCount(args.Count, span);
            pipelineInput?.AttachAsPipelineInput(context, span);
        }

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
                "encoding" => PyString.FromString(EncodingName),
                "errors" => PyString.FromString(ErrorsName),
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
            _ = Complete(timeoutMilliseconds: null, null);
            return false;
        }

        public async ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            CloseEndpoints();
            _ = await CompleteAsync(timeoutMilliseconds: null, null).ConfigureAwait(false);
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var returnCode = _result is null ? "None" : _result.ReturnCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return PyString.FromString($"<Popen: returncode: {returnCode} args: {PyRendering.ToPythonString(_args, context)}>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public PyString GetOutput(bool standardError, LythonSourceSpan? span)
        {
            _ = Complete(timeoutMilliseconds: null, span);
            return standardError ? _decodedStderr ?? PyString.Empty : _decodedStdout ?? PyString.Empty;
        }

        public async ValueTask<PyString> GetOutputAsync(bool standardError, LythonSourceSpan? span)
        {
            _ = await CompleteAsync(timeoutMilliseconds: null, span).ConfigureAwait(false);
            return standardError ? _decodedStderr ?? PyString.Empty : _decodedStdout ?? PyString.Empty;
        }

        public PipelinePayload CompleteForPipeline(
            int? timeoutMilliseconds,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            _ = Complete(timeoutMilliseconds, span, completionPath);
            var output = _decodedStdout ?? PyString.Empty;
            return new PipelinePayload(output.Utf8Bytes, _cumulativePipelineOutputBytes);
        }

        public async ValueTask<PipelinePayload> CompleteForPipelineAsync(
            int? timeoutMilliseconds,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            _ = await CompleteAsync(timeoutMilliseconds, span, completionPath).ConfigureAwait(false);
            var output = _decodedStdout ?? PyString.Empty;
            return new PipelinePayload(output.Utf8Bytes, _cumulativePipelineOutputBytes);
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
            if (input is PyNone or null)
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

            if (!PyStringOps.TryAsString(input, out var text))
            {
                throw new LythonRuntimeException("TypeError", "Popen.communicate(input=...) expects a string or None.", span);
            }

            _ = _stdin.Write(text, span);
        }

        private LythonSubprocessResult Complete(int? timeoutMilliseconds, LythonSourceSpan? span)
            => Complete(timeoutMilliseconds, span, new HashSet<PyPopen>());

        private LythonSubprocessResult Complete(
            int? timeoutMilliseconds,
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
                var request = BuildCompletionRequest(timeoutMilliseconds, span, completionPath);
                _context.RegisterHostCall(span);
                var result = _context.RunSubprocess(request, span);
                DecodeCompletion(result, span ?? PopenSyntheticSpan);
                _result = result;
                return result;
            }
            catch (LythonRuntimeException ex) when (ex.InnerException is TimeoutException)
            {
                var timeout = CreateTimeoutExpired(timeoutMilliseconds, span ?? PopenSyntheticSpan);
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

        private async ValueTask<LythonSubprocessResult> CompleteAsync(int? timeoutMilliseconds, LythonSourceSpan? span)
            => await CompleteAsync(timeoutMilliseconds, span, new HashSet<PyPopen>()).ConfigureAwait(false);

        private async ValueTask<LythonSubprocessResult> CompleteAsync(
            int? timeoutMilliseconds,
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
                var request = await BuildCompletionRequestAsync(timeoutMilliseconds, span, completionPath).ConfigureAwait(false);
                _context.RegisterHostCall(span);
                var result = await _context.RunSubprocessAsync(request, span).ConfigureAwait(false);
                DecodeCompletion(result, span ?? PopenSyntheticSpan);
                _result = result;
                return result;
            }
            catch (LythonRuntimeException ex) when (ex.InnerException is TimeoutException)
            {
                var timeout = CreateTimeoutExpired(timeoutMilliseconds, span ?? PopenSyntheticSpan);
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
            int? timeoutMilliseconds,
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
                var pipeline = _pipelineInput.TakePipelinePayload(timeoutMilliseconds, span, completionPath);
                input = pipeline.Input;
                _cumulativePipelineOutputBytes = pipeline.CumulativeOutputBytes;
            }

            return _request with
            {
                StandardInputUtf8 = input,
                Timeout = timeoutMilliseconds is { } timeoutValue
                    ? TimeSpan.FromMilliseconds(timeoutValue)
                    : null,
            };
        }

        private async ValueTask<LythonSubprocessRequest> BuildCompletionRequestAsync(
            int? timeoutMilliseconds,
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
                    .TakePipelinePayloadAsync(timeoutMilliseconds, span, completionPath)
                    .ConfigureAwait(false);
                input = pipeline.Input;
                _cumulativePipelineOutputBytes = pipeline.CumulativeOutputBytes;
            }

            return _request with
            {
                StandardInputUtf8 = input,
                Timeout = timeoutMilliseconds is { } timeoutValue
                    ? TimeSpan.FromMilliseconds(timeoutValue)
                    : null,
            };
        }

        private void DecodeCompletion(LythonSubprocessResult result, LythonSourceSpan span)
        {
            if (_stdout is not null)
            {
                _decodedStdout = DecodeSubprocessOutput(result.StandardOutputUtf8, _request.TextEncoding, _request.TextErrorMode, _context, span);
            }

            if (_stderr is not null)
            {
                _decodedStderr = DecodeSubprocessOutput(result.StandardErrorUtf8, _request.TextEncoding, _request.TextErrorMode, _context, span);
            }

            var ownCapturedBytes =
                (_request.StandardOutput == LythonSubprocessStreamMode.Pipe ? (long)result.StandardOutputUtf8.Length : 0L) +
                (_request.StandardError == LythonSubprocessStreamMode.Pipe ? (long)result.StandardErrorUtf8.Length : 0L);
            _cumulativePipelineOutputBytes = checked(_cumulativePipelineOutputBytes + ownCapturedBytes);
            if (_context.Limits.MaxStringLength is { } maximum && _cumulativePipelineOutputBytes > maximum)
            {
                throw RuntimeErrors.Runtime($"maximum cumulative Popen pipeline output exceeded ({maximum})", span);
            }
        }

        private LythonRuntimeException CreateTimeoutExpired(int? timeoutMilliseconds, LythonSourceSpan span)
        {
            object timeout = timeoutMilliseconds is { } value ? new BigInteger(value) : PyNone.Instance;
            var payload = CreateTimeoutExpiredPayload(
                _args,
                timeout,
                PyNone.Instance,
                PyNone.Instance,
                _context,
                span);
            return new LythonRuntimeException(
                "TimeoutExpired",
                $"Command timed out after {(timeout is PyNone ? "None" : timeout)}.",
                span,
                innerException: null,
                payload: payload);
        }

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

        private static int? ReadPopenTimeout(object[] arguments, int index, string owner, LythonSourceSpan span)
        {
            var value = GetArgument(arguments, index);
            if (value is PyNone or null)
            {
                return null;
            }

            var timeout = ParseOptionalInt(value, $"{owner}(..., timeout=...)", span);
            if (timeout < 0)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(..., timeout=...) expects a non-negative value.", span);
            }

            return timeout;
        }

        private static object UnsupportedMethod(string name, string capability)
            => new UnsupportedSubprocessCallable(
                name,
                $"{name}() is unsupported because Lython's buffered Popen facade has no {capability}.");
    }

}
