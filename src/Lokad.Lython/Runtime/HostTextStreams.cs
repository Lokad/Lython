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

    public bool IsAvailable => _input is not null;

    public PyString ReadAll(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input is not available.", span);
        }

        var utf8 = AwaitHost(() => _input.ReadToEndUtf8Async(_state.Limits.CancellationToken), "stdin.read", span);
        CheckInputLimit(utf8.Length, span);
        return LythonRuntime.DecodeUtf8Text(utf8, _state.MemoryGovernor, span);
    }

    public async ValueTask<PyString> ReadAllAsync(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input is not available.", span);
        }

        var utf8 = await AwaitHostAsync(() => _input.ReadToEndUtf8Async(_state.Limits.CancellationToken), "stdin.read", span).ConfigureAwait(false);
        CheckInputLimit(utf8.Length, span);
        return LythonRuntime.DecodeUtf8Text(utf8, _state.MemoryGovernor, span);
    }

    public PyString ReadLine(LythonSourceSpan? span)
    {
        if (_input is null)
        {
            throw new LythonRuntimeException("RuntimeError", "standard input is not available.", span);
        }

        var utf8 = AwaitHost(() => _input.ReadLineUtf8Async(_state.Limits.CancellationToken), "stdin.readline", span);
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

        var utf8 = await AwaitHostAsync(() => _input.ReadLineUtf8Async(_state.Limits.CancellationToken), "stdin.readline", span).ConfigureAwait(false);
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

    private static T AwaitHost<T>(Func<ValueTask<T>> operation, string name, LythonSourceSpan? span)
    {
        try
        {
            var valueTask = operation();
            if (valueTask.IsCompletedSuccessfully)
            {
                return valueTask.Result;
            }

            var task = valueTask.AsTask();
            if (!task.IsCompleted)
            {
                throw RuntimeErrors.Runtime($"{name} completed asynchronously; use RunAsync with asynchronous hosts.", span);
            }

            if (task.IsCanceled)
            {
                throw RuntimeErrors.Runtime("execution canceled", span);
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? new InvalidOperationException($"{name} failed.");
            }

            return task.Result;
        }
        catch (OperationCanceledException)
        {
            throw RuntimeErrors.Runtime("execution canceled", span);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw RuntimeErrors.Host(name, ex, span);
        }
    }

    private static async ValueTask<T> AwaitHostAsync<T>(Func<ValueTask<T>> operation, string name, LythonSourceSpan? span)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw RuntimeErrors.Runtime("execution canceled", span);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw RuntimeErrors.Host(name, ex, span);
        }
    }
}

internal sealed class HostTextOutputHandle : IPyRenderableValue
{
    private readonly ILythonTextOutput? _output;
    private readonly Utf8ValueBuilder? _capture;
    private readonly string _name;
    private readonly ExecutionState _state;

    public HostTextOutputHandle(ILythonTextOutput? output, Utf8ValueBuilder? capture, string name, ExecutionState state)
    {
        _output = output;
        _capture = capture;
        _name = name;
        _state = state;
    }

    public bool IsAvailable => _output is not null || _capture is not null;

    public BigInteger Write(PyString text, LythonSourceSpan? span)
    {
        if (_output is null && _capture is null)
        {
            throw new LythonRuntimeException("RuntimeError", $"{_name} is not available.", span);
        }

        if (_capture is not null)
        {
            _capture.Append(text);
        }

        if (_output is not null)
        {
            AwaitHost(() => _output.WriteUtf8Async(text.Utf8Bytes, _state.Limits.CancellationToken), _name + ".write", span);
        }

        return new BigInteger(text.Length);
    }

    public async ValueTask<BigInteger> WriteAsync(PyString text, LythonSourceSpan? span)
    {
        if (_output is null && _capture is null)
        {
            throw new LythonRuntimeException("RuntimeError", $"{_name} is not available.", span);
        }

        if (_capture is not null)
        {
            _capture.Append(text);
        }

        if (_output is not null)
        {
            await AwaitHostAsync(() => _output.WriteUtf8Async(text.Utf8Bytes, _state.Limits.CancellationToken), _name + ".write", span).ConfigureAwait(false);
        }

        return new BigInteger(text.Length);
    }

    public object Flush(LythonSourceSpan? span)
    {
        if (_output is null)
        {
            if (_capture is not null)
            {
                return PyNone.Instance;
            }

            throw new LythonRuntimeException("RuntimeError", $"{_name} is not available.", span);
        }

        AwaitHost(() => _output.FlushAsync(_state.Limits.CancellationToken), _name + ".flush", span);
        return PyNone.Instance;
    }

    public async ValueTask<object> FlushAsync(LythonSourceSpan? span)
    {
        if (_output is null)
        {
            if (_capture is not null)
            {
                return PyNone.Instance;
            }

            throw new LythonRuntimeException("RuntimeError", $"{_name} is not available.", span);
        }

        await AwaitHostAsync(() => _output.FlushAsync(_state.Limits.CancellationToken), _name + ".flush", span).ConfigureAwait(false);
        return PyNone.Instance;
    }

    public PyString RenderPython(PyRenderingContext context) => PyString.FromString(_name);

    public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

    private static void AwaitHost(Func<ValueTask> operation, string name, LythonSourceSpan? span)
    {
        try
        {
            var task = operation().AsTask();
            if (!task.IsCompleted)
            {
                throw RuntimeErrors.Runtime($"{name} completed asynchronously; use RunAsync with asynchronous hosts.", span);
            }

            if (task.IsCanceled)
            {
                throw RuntimeErrors.Runtime("execution canceled", span);
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? new InvalidOperationException($"{name} failed.");
            }
        }
        catch (OperationCanceledException)
        {
            throw RuntimeErrors.Runtime("execution canceled", span);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw RuntimeErrors.Host(name, ex, span);
        }
    }

    private static async ValueTask AwaitHostAsync(Func<ValueTask> operation, string name, LythonSourceSpan? span)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw RuntimeErrors.Runtime("execution canceled", span);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw RuntimeErrors.Host(name, ex, span);
        }
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
