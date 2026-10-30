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

    internal void WriteHostBytes(string path,ReadOnlyMemory<byte> payload,LythonSourceSpan? span)
        => WriteHostBytesAsync(path,payload,span,false).GetAwaiter().GetResult();

    internal ValueTask WriteHostBytesAsync(string path,ReadOnlyMemory<byte> payload,LythonSourceSpan? span)
        => WriteHostBytesAsync(path,payload,span,true);

    private async ValueTask WriteHostBytesAsync(string path,ReadOnlyMemory<byte> payload,LythonSourceSpan? span,bool asynchronous)
    {
        CheckExecution(span);
        using var shell=MemoryGovernor.ReserveTemporary(128,span);
        var file=new OpenedByteFile(this);
        try
        {
            await file.OpenAsync(path,new LythonFileOpenOptions(LythonFileAccess.Write,create:true,truncate:true),span,asynchronous).ConfigureAwait(false);
            await file.WriteAsync(payload,span,asynchronous).ConfigureAwait(false);
            await file.FlushAsync(span,asynchronous).ConfigureAwait(false);
            await file.CloseAsync(span,asynchronous).ConfigureAwait(false);
            CheckExecution(span);
        }
        catch {await file.CloseAfterFailureAsync(span,asynchronous).ConfigureAwait(false);throw;}
    }
}
