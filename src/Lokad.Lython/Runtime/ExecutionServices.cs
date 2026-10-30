using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed class ExecutionServices
{
    public ExecutionServices(ExecutionState state)
    {
        State = state;
        Guards = state.Guards;
    }

    public ExecutionState State { get; }

    public ExecutionGuards Guards { get; }


    public PyException? CurrentException { get; private set; }

    public ILythonHost Host => State.Host;

    public LythonRuntime.ExecutionLimits Limits => State.Limits;

    public MemoryGovernor MemoryGovernor => State.MemoryGovernor;

    internal DateTimeOffset ReadHostLocalNow()
        => HostOperation.Invoke(() => Host.LocalNow, "local_clock", null, MemoryGovernor);

    internal DateTimeOffset ReadHostUtcNow()
        => HostOperation.Invoke(() => Host.UtcNow, "utc_clock", null, MemoryGovernor);

    public void CheckExecution(LythonSourceSpan? span) => Guards.CheckExecution(span);

    public void EnterFunctionCall(LythonSourceSpan? span) => Guards.EnterFunctionCall(span);

    public void LeaveFunctionCall() => Guards.LeaveFunctionCall();

    public void EnterInterpreterFrame(LythonSourceSpan? span) => Guards.EnterInterpreterFrame(span);

    public void LeaveInterpreterFrame() => Guards.LeaveInterpreterFrame();

    public PyException? SetCurrentException(PyException? exception)
    {
        var previous = CurrentException;
        CurrentException = exception;
        return previous;
    }

    // Runtime file owners need host authority and limits, not the lexical frame
    // that opened them. Keep the existing host boundary and path policy here.
    internal string ContainedHostPath(string path, LythonSourceSpan? span)
        => PathOps.RequireContainedPath(PathOps.Normalize(path, Host.Cwd), span);

    internal ValueTask<ILythonByteFile> OpenFileAsync(string path, LythonFileOpenOptions options,
        LythonSourceSpan? span, bool asynchronous)
    {
        var request = PathOps.HostPathRequest(path, Host.Cwd, span);
        return asynchronous
            ? HostOperation.AwaitAsync(() => Host.OpenFileAsync(request, options, MemoryGovernor.Budget, Limits.CancellationToken),
                "open_file", span, MemoryGovernor)
            : ValueTask.FromResult(HostOperation.Await(Host,
                () => Host.OpenFileAsync(request, options, MemoryGovernor.Budget, Limits.CancellationToken),
                "open_file", span, MemoryGovernor));
    }

    public ReadOnlyMemory<byte> ReadHostBytesRange(string path, long offset, int count, LythonSourceSpan? span)
        => HostOperation.Await(Host, () => Host.ReadBytesRangeAsync(ContainedHostPath(path, span), offset, count, Limits.CancellationToken), "read_bytes", span, MemoryGovernor);

    public ValueTask<ReadOnlyMemory<byte>> ReadHostBytesRangeAsync(string path, long offset, int count, LythonSourceSpan? span)
        => HostOperation.AwaitAsync(() => Host.ReadBytesRangeAsync(ContainedHostPath(path, span), offset, count, Limits.CancellationToken), "read_bytes", span, MemoryGovernor);

    public void WriteHostBytes(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
        => HostOperation.Await(Host, () => Host.WriteBytesAsync(ContainedHostPath(path, span), payload, Limits.CancellationToken), "write_bytes", span, MemoryGovernor);

    public ValueTask WriteHostBytesAsync(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
        => HostOperation.AwaitAsync(() => Host.WriteBytesAsync(ContainedHostPath(path, span), payload, Limits.CancellationToken), "write_bytes", span, MemoryGovernor);

    public void AppendHostBytes(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
        => HostOperation.Await(Host, () => Host.AppendBytesAsync(ContainedHostPath(path, span), payload, Limits.CancellationToken), "append_bytes", span, MemoryGovernor);

    public ValueTask AppendHostBytesAsync(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
        => HostOperation.AwaitAsync(() => Host.AppendBytesAsync(ContainedHostPath(path, span), payload, Limits.CancellationToken), "append_bytes", span, MemoryGovernor);

    public ReadOnlyMemory<byte> ReadTextUtf8Range(string path, long offset, int count, LythonSourceSpan? span)
        => HostOperation.Await(Host, () => Host.ReadTextUtf8RangeAsync(ContainedHostPath(path, span), offset, count, Limits.CancellationToken), "read_text", span, MemoryGovernor);

    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8RangeAsync(string path, long offset, int count, LythonSourceSpan? span)
        => HostOperation.AwaitAsync(() => Host.ReadTextUtf8RangeAsync(ContainedHostPath(path, span), offset, count, Limits.CancellationToken), "read_text", span, MemoryGovernor);

    public void WriteTextUtf8(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
        => HostOperation.Await(Host, () => Host.WriteTextUtf8Async(ContainedHostPath(path, span), utf8, Limits.CancellationToken), "write_text", span, MemoryGovernor);

    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
        => HostOperation.AwaitAsync(() => Host.WriteTextUtf8Async(ContainedHostPath(path, span), utf8, Limits.CancellationToken), "write_text", span, MemoryGovernor);

    public void AppendTextUtf8(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
        => HostOperation.Await(Host, () => Host.AppendTextUtf8Async(ContainedHostPath(path, span), utf8, Limits.CancellationToken), "append_text", span, MemoryGovernor);

    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
        => HostOperation.AwaitAsync(() => Host.AppendTextUtf8Async(ContainedHostPath(path, span), utf8, Limits.CancellationToken), "append_text", span, MemoryGovernor);
}
