namespace Lokad.Lython.Runtime;

internal sealed partial class ExecutionState
{
    public PyException? CurrentException { get; private set; }

    internal DateTimeOffset ReadHostLocalNow()
        => HostOperation.Invoke(() => Host.LocalNow, "local_clock", null, MemoryGovernor);

    internal DateTimeOffset ReadHostUtcNow()
        => HostOperation.Invoke(() => Host.UtcNow, "utc_clock", null, MemoryGovernor);

    public PyException? SetCurrentException(PyException? exception)
    {
        var previous = CurrentException;
        CurrentException = exception;
        return previous;
    }

    // Runtime file owners retain shared authority, budget and token, not the lexical frame
    // that opened them. Keep the existing host boundary and path policy here.
    internal ValueTask<ILythonByteFile> OpenFileAsync(string path, LythonFileOpenOptions options,
        LythonSourceSpan? span, bool asynchronous)
    {
        var request = PathOps.HostPathRequest(path, Host.Cwd, span, CancellationToken);
        return asynchronous
            ? HostOperation.AwaitAsync(() => Host.OpenFileAsync(request, options, MemoryGovernor.Budget, CancellationToken),
                "open_file", span, MemoryGovernor)
            : ValueTask.FromResult(HostOperation.Await(Host,
                () => Host.OpenFileAsync(request, options, MemoryGovernor.Budget, CancellationToken),
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
