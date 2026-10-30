namespace Lokad.Lython.Tests.Harness;

/// <summary>
/// Records every host-boundary effect (operation plus path) while forwarding to an
/// inner host, so budget tests can assert exact host traces instead of only
/// success or failure outcomes. Byte-counted transfers additionally support
/// deterministic host-traffic measurements. Wrapping preserves the inner
/// synchronous capability.
/// </summary>
internal sealed class TracingLythonHost : ILythonHost, ILythonSynchronousHostCapability
{
    private readonly ILythonHost _inner;

    public TracingLythonHost(ILythonHost inner)
    {
        _inner = inner;
    }

    public List<string> Trace { get; } = new();

    public List<(string Operation, LythonPathRequest Request)> MetadataRequests { get; } = [];

    public sealed record HostTransfer(string Operation, string Path, int Bytes);

    public List<HostTransfer> Transfers { get; } = new();

    public ILythonHost Inner => _inner;

    public string Cwd => _inner.Cwd;

    public bool CompletesSynchronously
        => _inner is ILythonSynchronousHostCapability sync && sync.CompletesSynchronously;

    public DateTimeOffset LocalNow
    {
        get
        {
            Trace.Add("clock");
            return _inner.LocalNow;
        }
    }

    public DateTimeOffset UtcNow
    {
        get
        {
            Trace.Add("clock-utc");
            return _inner.UtcNow;
        }
    }

    public ILythonTextInput? StandardInput => _inner.StandardInput;

    public ILythonTextOutput? StandardOutput => _inner.StandardOutput;

    public ILythonTextOutput? StandardError => _inner.StandardError;

    public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;

    public ILythonTiming? Timing => _inner.Timing;

    public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request,LythonFileOpenOptions options,
        ILythonMemoryBudget budget,CancellationToken token)
    {
        var path=request.Path;
        Trace.Add("open:"+path);
        var file=await _inner.OpenFileAsync(request,options,budget,token);
        return new TestByteFile(file)
        {
            Read=async (buffer,current)=>
            {
                Trace.Add("read:"+path);
                var count=await file.ReadAsync(buffer,current);
                Transfers.Add(new HostTransfer("read",path,count));return count;
            },
            Write=async (buffer,current)=>
            {
                Trace.Add("write:"+path);
                var result=await file.WriteAsync(buffer,current);
                Transfers.Add(new HostTransfer("write",path,result.AcceptedBytes??0));return result;
            },
        };
    }

    public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken cancellationToken)
    {
        Trace.Add("exists:" + path.Path);
        MetadataRequests.Add(("exists", path));
        return _inner.ExistsAsync(path, cancellationToken);
    }

    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
    {
        Trace.Add("listdir:" + path);
        return _inner.ListDirAsync(path, cancellationToken);
    }

    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
    {
        Trace.Add("mkdir:" + path);
        return _inner.MkDirAsync(path, cancellationToken);
    }

    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
    {
        Trace.Add("remove:" + path);
        return _inner.RemoveAsync(path, cancellationToken);
    }

    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        Trace.Add("copy:" + source + ">" + destination);
        return _inner.CopyAsync(source, destination, cancellationToken);
    }

    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        Trace.Add("move:" + source + ">" + destination);
        return _inner.MoveAsync(source, destination, cancellationToken);
    }

    public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken cancellationToken)
    {
        Trace.Add("stat:" + path.Path);
        MetadataRequests.Add(("stat", path));
        return _inner.StatAsync(path, cancellationToken);
    }
}
