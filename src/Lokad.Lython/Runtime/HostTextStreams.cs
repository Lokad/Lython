using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class HostTextInputHandle : IPyRenderableValue
{
    // The host capability/run-state are back-pointers; a partially consumed
    // admitted line is shared by scalar, readline and read-to-end operations.

    private readonly ILythonTextInput? _input;
    private readonly ExecutionState _state;

    internal bool IsAvailable => _input is not null;

    private PyString _pendingLine = PyString.Empty;
    private int _pendingOffset;

    internal async ValueTask<int> ReadSymbolAsync(LythonSourceSpan span, bool asynchronous)
    {
        if (_input is null) throw RuntimeErrors.Capability("standard input is not available.", span);
        _state.Guards.CheckExecution(span);
        if (_pendingOffset >= _pendingLine.Utf8Bytes.Length)
        {
            var bytes = asynchronous
                ? await HostOperation.AwaitAsync(() => _input.ReadLineUtf8Async(_state.Limits.CancellationToken), "stdin.readline", span).ConfigureAwait(false)
                : HostOperation.Await(_input, () => _input.ReadLineUtf8Async(_state.Limits.CancellationToken), "stdin.readline", span);
            if (bytes is null || bytes.Value.IsEmpty) return -1;
            _pendingLine = LythonRuntime.DecodeUtf8Text(bytes.Value, _state.MemoryGovernor, span, _state.Guards);
            _pendingOffset = 0;
        }
        Rune.DecodeFromUtf8(_pendingLine.Utf8Bytes.Span[_pendingOffset..], out var rune, out var consumed);
        _pendingOffset += consumed;
        return rune.Value;
    }

    private bool TakePendingLine(out PyString line)
    {
        line = _pendingLine.SliceByByteRange(_pendingOffset, _pendingLine.Utf8Bytes.Length);
        _pendingLine = PyString.Empty;
        _pendingOffset = 0;
        return !line.Utf8Bytes.IsEmpty;
    }

    private PyString PrependPending(PyString tail, LythonSourceSpan? span)
    {
        if (_pendingOffset >= _pendingLine.Utf8Bytes.Length) return tail;
        var builder = new GovernedByteBuilder(_state.MemoryGovernor, span);
        try
        {
            builder.Append(_pendingLine.Utf8Bytes.Span[_pendingOffset..]);
            builder.Append(tail);
            var result = builder.ToPyStringAndRelease();
            _pendingLine = PyString.Empty; _pendingOffset = 0;
            return result;
        }
        finally { builder.Release(); }
    }

    public HostTextInputHandle(ILythonTextInput? input, ExecutionState state)
    {
        _input = input;
        _state = state;
    }

    public PyString ReadAll(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw RuntimeErrors.Capability("standard input is not available.", span);
        }

        _state.Guards.CheckExecution(span);
        var utf8 = HostOperation.Await(_input, () => _input.ReadToEndUtf8Async(_state.Limits.CancellationToken), "stdin.read", span);
        return PrependPending(LythonRuntime.DecodeUtf8Text(utf8, _state.MemoryGovernor, span, _state.Guards), span);
    }

    public async ValueTask<PyString> ReadAllAsync(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw RuntimeErrors.Capability("standard input is not available.", span);
        }

        _state.Guards.CheckExecution(span);
        var utf8 = await HostOperation.AwaitAsync(() => _input.ReadToEndUtf8Async(_state.Limits.CancellationToken), "stdin.read", span).ConfigureAwait(false);
        return PrependPending(LythonRuntime.DecodeUtf8Text(utf8, _state.MemoryGovernor, span, _state.Guards), span);
    }

    public PyString ReadLine(LythonSourceSpan? span)
    {
        if (TakePendingLine(out var pending)) return pending;
        if (_input is null)
        {
            throw RuntimeErrors.Capability("standard input is not available.", span);
        }

        _state.Guards.CheckExecution(span);
        var utf8 = HostOperation.Await(_input, () => _input.ReadLineUtf8Async(_state.Limits.CancellationToken), "stdin.readline", span);
        if (utf8 is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input reached end-of-stream.", span);
        }
        return LythonRuntime.DecodeUtf8Text(utf8.Value, _state.MemoryGovernor, span, _state.Guards);
    }

    public async ValueTask<PyString> ReadLineAsync(LythonSourceSpan? span)
    {
        if (TakePendingLine(out var pending)) return pending;
        if (_input is null)
        {
            throw RuntimeErrors.Capability("standard input is not available.", span);
        }

        _state.Guards.CheckExecution(span);
        var utf8 = await HostOperation.AwaitAsync(() => _input.ReadLineUtf8Async(_state.Limits.CancellationToken), "stdin.readline", span).ConfigureAwait(false);
        if (utf8 is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input reached end-of-stream.", span);
        }
        return LythonRuntime.DecodeUtf8Text(utf8.Value, _state.MemoryGovernor, span, _state.Guards);
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<stdin>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

}

internal sealed class HostTextOutputHandle : IPyRenderableValue
{
    // Captured bytes belong to the persistent run builder, not guest children.

    private readonly OutputDestination _destination;
    private readonly string _name;
    private readonly ExecutionState _state;

    public HostTextOutputHandle(ILythonTextOutput output, string name, ExecutionState state)
        : this(new HostOnlyOutputDestination(output), name, state)
    {
    }

    public HostTextOutputHandle(ILythonTextOutput? output, GovernedByteBuilder capture, string name, ExecutionState state)
        : this(new CapturedOutputDestination(capture, output), name, state)
    {
    }

    private HostTextOutputHandle(OutputDestination destination, string name, ExecutionState state)
    {
        _destination = destination;
        _name = name;
        _state = state;
    }

    public BigInteger Write(PyString text, LythonSourceSpan? span)
    {
        if (_destination.Output is { } output)
        {
            HostOperation.RequireSynchronousCapability(output, _name + ".write", span);
            _state.Guards.CheckExecution(span);
        }

        if (_destination.Capture is { } capture)
        {
            capture.Append(text);
        }

        if (_destination.Output is { } hostOutput)
        {
            HostOperation.Await(hostOutput, () => hostOutput.WriteUtf8Async(text.Utf8Bytes, _state.Limits.CancellationToken), _name + ".write", span);
        }

        return new BigInteger(text.Length);
    }

    public async ValueTask<BigInteger> WriteAsync(PyString text, LythonSourceSpan? span)
    {
        if (_destination.Capture is { } capture)
        {
            capture.Append(text);
        }

        if (_destination.Output is { } output)
        {
            _state.Guards.CheckExecution(span);
            await HostOperation.AwaitAsync(() => output.WriteUtf8Async(text.Utf8Bytes, _state.Limits.CancellationToken), _name + ".write", span).ConfigureAwait(false);
        }

        return new BigInteger(text.Length);
    }

    public object Flush(LythonSourceSpan? span)
    {
        if (_destination.Output is not { } output)
        {
            return PyNone.Instance;
        }

        _state.Guards.CheckExecution(span);
        HostOperation.Await(output, () => output.FlushAsync(_state.Limits.CancellationToken), _name + ".flush", span);
        return PyNone.Instance;
    }

    public async ValueTask<object> FlushAsync(LythonSourceSpan? span)
    {
        if (_destination.Output is not { } output)
        {
            return PyNone.Instance;
        }

        _state.Guards.CheckExecution(span);
        await HostOperation.AwaitAsync(() => output.FlushAsync(_state.Limits.CancellationToken), _name + ".flush", span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString(_name);

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private abstract record OutputDestination
    {
        /// <summary>
        /// Gets the governed capture sink, or null for host-only handles. The
        /// builder is owned by its creator (ExecutionState for standard streams,
        /// released with the run); the handle only appends and never releases it.
        /// </summary>
        public abstract GovernedByteBuilder? Capture { get; }

        /// <summary>
        /// Gets the host-owned text stream, or null when the host supplies none.
        /// The handle registers host calls but never disposes the stream.
        /// </summary>
        public abstract ILythonTextOutput? Output { get; }
    }

    private sealed record CapturedOutputDestination(
        GovernedByteBuilder CapturedBytes,
        ILythonTextOutput? HostOutput) : OutputDestination
    {
        public override GovernedByteBuilder Capture => CapturedBytes;

        public override ILythonTextOutput? Output => HostOutput;
    }

    private sealed record HostOnlyOutputDestination(ILythonTextOutput HostOutput) : OutputDestination
    {
        public override GovernedByteBuilder? Capture => null;

        public override ILythonTextOutput Output => HostOutput;
    }

}

internal sealed class PyCompletedProcess : IPyRenderableValue
{
    public PyCompletedProcess(object args, BigInteger returnCode, object stdout, object stderr)
    {
        Args = args;
        ReturnCode = returnCode;
        Stdout = stdout;
        Stderr = stderr;
    }

    public object Args { get; }

    public BigInteger ReturnCode { get; }

    public object Stdout { get; }

    public object Stderr { get; }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString($"CompletedProcess(returncode={ReturnCode})");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
}
