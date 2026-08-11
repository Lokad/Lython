using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class HostTextInputHandle : IPyRenderableValue
{
    private readonly ILythonTextInput? _input;
    private readonly ExecutionState _state;

    public HostTextInputHandle(ILythonTextInput? input, ExecutionState state)
    {
        _input = input;
        _state = state;
    }

    public PyString ReadAll(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input is not available.", span);
        }

        _state.BudgetGuards.RegisterHostCall(span);
        var utf8 = HostOperation.Await(_input, () => _input.ReadToEndUtf8Async(_state.Limits.CancellationToken), "stdin.read", span);
        CheckInputLimit(utf8.Length, span);
        return LythonRuntime.DecodeUtf8Text(utf8, _state.MemoryGovernor, span);
    }

    public async ValueTask<PyString> ReadAllAsync(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input is not available.", span);
        }

        _state.BudgetGuards.RegisterHostCall(span);
        var utf8 = await HostOperation.AwaitAsync(() => _input.ReadToEndUtf8Async(_state.Limits.CancellationToken), "stdin.read", span).ConfigureAwait(false);
        CheckInputLimit(utf8.Length, span);
        return LythonRuntime.DecodeUtf8Text(utf8, _state.MemoryGovernor, span);
    }

    public PyString ReadLine(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input is not available.", span);
        }

        _state.BudgetGuards.RegisterHostCall(span);
        var utf8 = HostOperation.Await(_input, () => _input.ReadLineUtf8Async(_state.Limits.CancellationToken), "stdin.readline", span);
        if (utf8 is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input reached end-of-stream.", span);
        }

        CheckInputLimit(utf8.Value.Length, span);
        return LythonRuntime.DecodeUtf8Text(utf8.Value, _state.MemoryGovernor, span);
    }

    public async ValueTask<PyString> ReadLineAsync(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input is not available.", span);
        }

        _state.BudgetGuards.RegisterHostCall(span);
        var utf8 = await HostOperation.AwaitAsync(() => _input.ReadLineUtf8Async(_state.Limits.CancellationToken), "stdin.readline", span).ConfigureAwait(false);
        if (utf8 is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input reached end-of-stream.", span);
        }

        CheckInputLimit(utf8.Value.Length, span);
        return LythonRuntime.DecodeUtf8Text(utf8.Value, _state.MemoryGovernor, span);
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString("<stdin>");

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private void CheckInputLimit(int length, LythonSourceSpan? span)
    {
        if (_state.Limits.MaxHostReadBytes is { } maxReadBytes && length > maxReadBytes)
        {
            throw RuntimeErrors.Runtime($"standard input read exceeded maximum bytes ({maxReadBytes})", span);
        }
    }

}

internal sealed class HostTextOutputHandle : IPyRenderableValue
{
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
            _state.BudgetGuards.RegisterHostCall(span);
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
            _state.BudgetGuards.RegisterHostCall(span);
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

        _state.BudgetGuards.RegisterHostCall(span);
        HostOperation.Await(output, () => output.FlushAsync(_state.Limits.CancellationToken), _name + ".flush", span);
        return PyNone.Instance;
    }

    public async ValueTask<object> FlushAsync(LythonSourceSpan? span)
    {
        if (_destination.Output is not { } output)
        {
            return PyNone.Instance;
        }

        _state.BudgetGuards.RegisterHostCall(span);
        await HostOperation.AwaitAsync(() => output.FlushAsync(_state.Limits.CancellationToken), _name + ".flush", span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString(_name);

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private abstract record OutputDestination
    {
        public abstract GovernedByteBuilder? Capture { get; }

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
