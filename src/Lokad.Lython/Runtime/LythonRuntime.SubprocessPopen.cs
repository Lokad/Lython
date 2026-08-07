using System.Collections;
using System.Numerics;
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
        if (context.Host.SubprocessRunner is null)
        {
            throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
        }

        ValidatePopenCompatibilityOptions(arguments, span);
        var pipelineInput = GetArgument(arguments, PopenStdinIndex) as PopenOutputStream;
        var shared = new object[15];
        Array.Fill(shared, PyNone.Instance);
        shared[SubprocessArgsIndex] = GetArgument(arguments, PopenArgsIndex);
        shared[SubprocessCwdIndex] = GetArgument(arguments, PopenCwdIndex);
        shared[SubprocessStdinIndex] = pipelineInput is null
            ? GetArgument(arguments, PopenStdinIndex)
            : new BigInteger(SubprocessPipe);
        shared[SubprocessStdoutIndex] = GetArgument(arguments, PopenStdoutIndex);
        shared[SubprocessStderrIndex] = GetArgument(arguments, PopenStderrIndex);
        shared[SubprocessShellIndex] = GetArgument(arguments, PopenShellIndex);
        shared[SubprocessTextIndex] = GetArgument(arguments, PopenTextIndex);
        shared[SubprocessEncodingIndex] = GetArgument(arguments, PopenEncodingIndex);
        shared[SubprocessErrorsIndex] = GetArgument(arguments, PopenErrorsIndex);
        shared[SubprocessEnvIndex] = GetArgument(arguments, PopenEnvIndex);
        shared[SubprocessUniversalNewlinesIndex] = GetArgument(arguments, PopenUniversalNewlinesIndex);

        var invocation = BuildSubprocessInvocation(
            shared,
            span,
            context,
            "subprocess.Popen",
            SubprocessCompletionKind.CompletedProcess,
            forcedCheck: false,
            forceStdoutPipe: false);
        var args = new PyList(
            invocation.Request.Args.Select<string, object>(PyString.FromString),
            context.MemoryGovernor,
            span);
        context.ObserveCollectionCount(args.Count, span);
        return new PyPopen(invocation.Request, args, pipelineInput, context, span);
    }

    private static void ValidatePopenCompatibilityOptions(object[] arguments, LythonSourceSpan span)
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

        public string EncodingName => _request.Encoding ?? "utf-8";

        public string ErrorsName => _request.Errors ?? "strict";

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
                "universal_newlines" => _request.TextMode,
                "communicate" => new BoundCallable(
                    (arguments, span, _) => Communicate(arguments, span),
                    async (arguments, span, _) => await CommunicateAsync(arguments, span).ConfigureAwait(false),
                    "Popen.communicate",
                    ["input", "timeout"],
                    0),
                "wait" => new BoundCallable(
                    (arguments, span, _) => Wait(arguments, span),
                    async (arguments, span, _) => await WaitAsync(arguments, span).ConfigureAwait(false),
                    "Popen.wait",
                    ["timeout"],
                    0),
                "poll" => new BoundCallable((arguments, span, _) => Poll(arguments, span), "Popen.poll", []),
                "send_signal" => UnsupportedMethod("Popen.send_signal", "signals and live process ownership"),
                "terminate" => UnsupportedMethod("Popen.terminate", "signals and live process ownership"),
                "kill" => UnsupportedMethod("Popen.kill", "signals and live process ownership"),
                "pid" => throw new LythonRuntimeException(
                    "NotImplementedError",
                    "Popen.pid is unavailable because Lython's buffered facade does not represent a live process.",
                    null),
                "__enter__" => new BoundCallable((arguments, span, _) => EnterBound(arguments, span), "Popen.__enter__", []),
                "__exit__" => new BoundCallable(
                    (arguments, span, _) => ExitBound(arguments, span),
                    async (arguments, span, _) => await ExitBoundAsync(arguments, span).ConfigureAwait(false),
                    "Popen.__exit__",
                    ["exc_type", "exc_value", "traceback"]),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
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

            return new PyTuple([_communicatedStdout, _communicatedStderr], _context.MemoryGovernor, span);
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
                TimeoutMilliseconds = timeoutMilliseconds,
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
                TimeoutMilliseconds = timeoutMilliseconds,
            };
        }

        private void DecodeCompletion(LythonSubprocessResult result, LythonSourceSpan span)
        {
            if (_stdout is not null)
            {
                _decodedStdout = DecodeSubprocessOutput(result.StandardOutputUtf8, _request.Encoding, _request.Errors, _context, span);
            }

            if (_stderr is not null)
            {
                _decodedStderr = DecodeSubprocessOutput(result.StandardErrorUtf8, _request.Encoding, _request.Errors, _context, span);
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

    private sealed class PopenInputStream : IPyDynamicAttributes, IPyAsyncContextManager, IPyRenderableValue
    {
        private readonly PyPopen _owner;
        private readonly ExecutionContext _context;
        private readonly GovernedByteBuilder _buffer;

        public PopenInputStream(PyPopen owner, ExecutionContext context)
        {
            _owner = owner;
            _context = context;
            _buffer = new GovernedByteBuilder(
                context.MemoryGovernor,
                allocationSpan: null,
                capacity: 0,
                maxLengthBytes: context.Limits.MaxStringLength,
                maxLengthOwner: "Popen stdin");
        }

        public bool IsClosed { get; private set; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "encoding" => PyString.FromString(_owner.EncodingName),
                "errors" => PyString.FromString(_owner.ErrorsName),
                "write" => new BoundCallable((arguments, span, _) => WriteBound(arguments, span), "Popen.stdin.write", ["s"]),
                "writelines" => new BoundCallable((arguments, span, _) => WriteLines(arguments, span), "Popen.stdin.writelines", ["lines"]),
                "flush" => new BoundCallable((arguments, span, _) => Flush(arguments, span), "Popen.stdin.flush", []),
                "close" => new BoundCallable((arguments, span, _) => CloseBound(arguments, span), "Popen.stdin.close", []),
                "writable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, writable: true), "Popen.stdin.writable", []),
                "readable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.readable", []),
                "seekable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.seekable", []),
                "isatty" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, writable: false), "Popen.stdin.isatty", []),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public BigInteger Write(PyString text, LythonSourceSpan span)
        {
            EnsureWritable(span);
            _buffer.Append(text);
            return new BigInteger(text.Length);
        }

        public ReadOnlyMemory<byte> SnapshotAndClose(LythonSourceSpan? span)
        {
            if (!IsClosed)
            {
                IsClosed = true;
            }

            return _buffer.WrittenMemory;
        }

        public void Close() => IsClosed = true;

        public void FlushValue(LythonSourceSpan span) => EnsureWritable(span);

        public object Enter()
        {
            EnsureWritable(null);
            return this;
        }

        public ValueTask<object> EnterAsync() => ValueTask.FromResult(Enter());

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            Close();
            return false;
        }

        public ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
            => ValueTask.FromResult(Exit(exceptionType, exceptionValue, traceback));

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<Popen stdin>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object WriteBound(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var text))
            {
                throw new LythonRuntimeException("TypeError", "Popen.stdin.write(s) expects one string.", span);
            }

            return Write(text, span);
        }

        private object WriteLines(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Popen.stdin.writelines(lines) expects one iterable.", span);
            }

            foreach (var item in ToSequence(arguments[0], span))
            {
                if (!PyStringOps.TryAsString(item, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "Popen.stdin.writelines(lines) expects strings.", span);
                }

                _ = Write(text, span);
            }

            return PyNone.Instance;
        }

        private object Flush(object[] arguments, LythonSourceSpan span)
        {
            RequirePopenNoArguments(arguments, "Popen.stdin.flush()", span);
            EnsureWritable(span);
            return PyNone.Instance;
        }

        private object CloseBound(object[] arguments, LythonSourceSpan span)
        {
            RequirePopenNoArguments(arguments, "Popen.stdin.close()", span);
            Close();
            return PyNone.Instance;
        }

        private object StreamPredicate(object[] arguments, LythonSourceSpan span, bool writable)
        {
            RequirePopenNoArguments(arguments, "Popen stdin stream predicate", span);
            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
            }

            return writable;
        }

        private void EnsureWritable(LythonSourceSpan? span)
        {
            if (IsClosed || _owner.IsCompleted)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
            }
        }
    }

    private sealed class PopenOutputStream : IPyDynamicAttributes, IPyAsyncContextManager, IPyAsyncIteratorValue, IPyRenderableValue
    {
        private readonly PyPopen _owner;
        private readonly bool _isStandardError;
        private readonly ExecutionContext _context;
        private int _cursorByte;
        private bool _claimedByPipeline;

        public PopenOutputStream(PyPopen owner, bool isStandardError, ExecutionContext context)
        {
            _owner = owner;
            _isStandardError = isStandardError;
            _context = context;
        }

        public bool IsClosed { get; private set; }

        public void AttachAsPipelineInput(ExecutionContext context, LythonSourceSpan span)
        {
            if (!ReferenceEquals(context, _context))
            {
                throw new LythonRuntimeException("ValueError", "Popen pipeline endpoints must belong to the same execution.", span);
            }

            if (_isStandardError)
            {
                throw new LythonRuntimeException("ValueError", "Popen(..., stdin=...) accepts a prior stdout pipe, not stderr.", span);
            }

            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "Cannot use a closed Popen stdout pipe as stdin.", span);
            }

            if (_claimedByPipeline)
            {
                throw new LythonRuntimeException("ValueError", "Popen stdout pipe is already connected to another process.", span);
            }

            if (_cursorByte != 0)
            {
                throw new LythonRuntimeException("ValueError", "Cannot connect a Popen stdout pipe after reading from it.", span);
            }

            _claimedByPipeline = true;
        }

        public PipelinePayload TakePipelinePayload(
            int? timeoutMilliseconds,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            try
            {
                var payload = _owner.CompleteForPipeline(timeoutMilliseconds, span, completionPath);
                MarkPipelineConsumed(payload.Input.Length);
                return payload;
            }
            catch
            {
                IsClosed = true;
                throw;
            }
        }

        public async ValueTask<PipelinePayload> TakePipelinePayloadAsync(
            int? timeoutMilliseconds,
            LythonSourceSpan? span,
            HashSet<PyPopen> completionPath)
        {
            try
            {
                var payload = await _owner
                    .CompleteForPipelineAsync(timeoutMilliseconds, span, completionPath)
                    .ConfigureAwait(false);
                MarkPipelineConsumed(payload.Input.Length);
                return payload;
            }
            catch
            {
                IsClosed = true;
                throw;
            }
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "closed" => IsClosed,
                "encoding" => PyString.FromString(_owner.EncodingName),
                "errors" => PyString.FromString(_owner.ErrorsName),
                "read" => new BoundCallable(
                    (arguments, span, _) => Read(arguments, span),
                    async (arguments, span, _) => await ReadAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.read",
                    ["size"],
                    0),
                "readline" => new BoundCallable(
                    (arguments, span, _) => ReadLine(arguments, span),
                    async (arguments, span, _) => await ReadLineAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.readline",
                    ["size"],
                    0),
                "readlines" => new BoundCallable(
                    (arguments, span, _) => ReadLines(arguments, span),
                    async (arguments, span, _) => await ReadLinesAsync(arguments, span).ConfigureAwait(false),
                    "Popen pipe.readlines",
                    ["hint"],
                    0),
                "close" => new BoundCallable((arguments, span, _) => CloseBound(arguments, span), "Popen pipe.close", []),
                "readable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, readable: true), "Popen pipe.readable", []),
                "writable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.writable", []),
                "seekable" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.seekable", []),
                "isatty" => new BoundCallable((arguments, span, _) => StreamPredicate(arguments, span, readable: false), "Popen pipe.isatty", []),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public IEnumerable<object> Iterate()
        {
            while (TryMoveNext(out var value))
            {
                yield return value;
            }
        }

        public async IAsyncEnumerable<object> IterateAsync()
        {
            while (true)
            {
                var (hasValue, value) = await TryMoveNextAsync().ConfigureAwait(false);
                if (!hasValue)
                {
                    yield break;
                }

                yield return value;
            }
        }

        public bool TryMoveNext([MaybeNullWhen(false)] out object value)
        {
            EnsureOpen(PopenSyntheticSpan);
            var line = ReadLineCore(_owner.GetOutput(_isStandardError, PopenSyntheticSpan), -1, PopenSyntheticSpan);
            if (line.Length == 0)
            {
                value = PyNone.Instance;
                return false;
            }

            value = line;
            return true;
        }

        public async ValueTask<(bool HasValue, object Value)> TryMoveNextAsync()
        {
            EnsureOpen(PopenSyntheticSpan);
            var content = await _owner.GetOutputAsync(_isStandardError, PopenSyntheticSpan).ConfigureAwait(false);
            var line = ReadLineCore(content, -1, PopenSyntheticSpan);
            return line.Length == 0 ? (false, PyNone.Instance) : (true, line);
        }

        public object Enter()
        {
            EnsureOpen(null);
            return this;
        }

        public ValueTask<object> EnterAsync() => ValueTask.FromResult(Enter());

        public bool Exit(object exceptionType, object exceptionValue, object traceback)
        {
            _ = exceptionType;
            _ = exceptionValue;
            _ = traceback;
            Close();
            return false;
        }

        public ValueTask<bool> ExitAsync(object exceptionType, object exceptionValue, object traceback)
            => ValueTask.FromResult(Exit(exceptionType, exceptionValue, traceback));

        public void Close() => IsClosed = true;

        public PyString ReadRemainingAfterCompletion(LythonSourceSpan span)
        {
            EnsureOpen(span);
            return ReadCore(_owner.GetOutput(_isStandardError, span), -1, span);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(_isStandardError ? "<Popen stderr>" : "<Popen stdout>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private object Read(object[] arguments, LythonSourceSpan span)
        {
            var size = ReadSize(arguments, "Popen pipe.read", span);
            EnsureOpen(span);
            return ReadCore(_owner.GetOutput(_isStandardError, span), size, span);
        }

        private async ValueTask<object> ReadAsync(object[] arguments, LythonSourceSpan span)
        {
            var size = ReadSize(arguments, "Popen pipe.read", span);
            EnsureOpen(span);
            var content = await _owner.GetOutputAsync(_isStandardError, span).ConfigureAwait(false);
            return ReadCore(content, size, span);
        }

        private object ReadLine(object[] arguments, LythonSourceSpan span)
        {
            var size = ReadSize(arguments, "Popen pipe.readline", span);
            EnsureOpen(span);
            return ReadLineCore(_owner.GetOutput(_isStandardError, span), size, span);
        }

        private async ValueTask<object> ReadLineAsync(object[] arguments, LythonSourceSpan span)
        {
            var size = ReadSize(arguments, "Popen pipe.readline", span);
            EnsureOpen(span);
            var content = await _owner.GetOutputAsync(_isStandardError, span).ConfigureAwait(false);
            return ReadLineCore(content, size, span);
        }

        private object ReadLines(object[] arguments, LythonSourceSpan span)
        {
            var hint = ReadSize(arguments, "Popen pipe.readlines", span);
            EnsureOpen(span);
            return ReadLinesCore(_owner.GetOutput(_isStandardError, span), hint, span);
        }

        private async ValueTask<object> ReadLinesAsync(object[] arguments, LythonSourceSpan span)
        {
            var hint = ReadSize(arguments, "Popen pipe.readlines", span);
            EnsureOpen(span);
            var content = await _owner.GetOutputAsync(_isStandardError, span).ConfigureAwait(false);
            return ReadLinesCore(content, hint, span);
        }

        private PyString ReadCore(PyString content, int size, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (_cursorByte >= content.Utf8Bytes.Length)
            {
                return PyString.Empty;
            }

            var end = size < 0
                ? content.Utf8Bytes.Length
                : ByteOffsetAfterRunes(content, _cursorByte, size);
            var value = content.SliceByByteRange(_cursorByte, end);
            _cursorByte = end;
            return value;
        }

        private PyString ReadLineCore(PyString content, int size, LythonSourceSpan span)
        {
            EnsureOpen(span);
            if (_cursorByte >= content.Utf8Bytes.Length)
            {
                return PyString.Empty;
            }

            var source = content.Utf8Bytes.Span;
            var newline = source[_cursorByte..].IndexOf((byte)'\n');
            var end = newline < 0 ? source.Length : _cursorByte + newline + 1;
            if (size >= 0)
            {
                end = Math.Min(end, ByteOffsetAfterRunes(content, _cursorByte, size));
            }

            var value = content.SliceByByteRange(_cursorByte, end);
            _cursorByte = end;
            return value;
        }

        private PyList ReadLinesCore(PyString content, int hint, LythonSourceSpan span)
        {
            var lines = new List<object>();
            var byteCount = 0;
            while (true)
            {
                var line = ReadLineCore(content, -1, span);
                if (line.Length == 0)
                {
                    break;
                }

                lines.Add(line);
                byteCount += line.Utf8Bytes.Length;
                if (hint > 0 && byteCount > hint)
                {
                    break;
                }
            }

            return new PyList(lines, _context.MemoryGovernor, span);
        }

        private object CloseBound(object[] arguments, LythonSourceSpan span)
        {
            RequirePopenNoArguments(arguments, "Popen pipe.close()", span);
            Close();
            return PyNone.Instance;
        }

        private object StreamPredicate(object[] arguments, LythonSourceSpan span, bool readable)
        {
            RequirePopenNoArguments(arguments, "Popen pipe stream predicate", span);
            EnsureOpen(span);
            return readable;
        }

        private void EnsureOpen(LythonSourceSpan? span)
        {
            if (IsClosed)
            {
                throw new LythonRuntimeException("ValueError", "I/O operation on closed file", span);
            }

            if (_claimedByPipeline)
            {
                throw new LythonRuntimeException("ValueError", "Popen stdout pipe is connected to another process.", span);
            }
        }

        private void MarkPipelineConsumed(int byteLength)
        {
            _cursorByte = byteLength;
            IsClosed = true;
        }

        private static int ReadSize(object[] arguments, string owner, LythonSourceSpan span)
        {
            if (arguments.Length == 0 || arguments[0] is PyNone)
            {
                return -1;
            }

            if (!PyNumberOps.TryAsInteger(arguments[0], out var value) || value < int.MinValue || value > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(size) expects an integer or None.", span);
            }

            return (int)value;
        }

        private static int ByteOffsetAfterRunes(PyString content, int startByte, int runeCount)
            => content.GetByteIndexAfterRunes(startByte, runeCount);
    }

    private readonly record struct PipelinePayload(ReadOnlyMemory<byte> Input, long CumulativeOutputBytes);

    private static void RequirePopenNoArguments(object[] arguments, string owner, LythonSourceSpan span)
    {
        if (arguments.Length != 0)
        {
            throw new LythonRuntimeException("TypeError", $"{owner} expects no arguments.", span);
        }
    }
}
