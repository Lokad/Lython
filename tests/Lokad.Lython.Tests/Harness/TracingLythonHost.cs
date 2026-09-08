namespace Lokad.Lython.Tests.Harness;

/// <summary>
/// Records every host-boundary effect (operation plus path) while forwarding to an
/// inner host, so budget tests can assert exact host traces instead of only
/// success or failure outcomes. Wrapping preserves the inner synchronous capability.
/// </summary>
internal sealed class TracingLythonHost : ILythonHost, ILythonSynchronousHostCapability
{
    private readonly ILythonHost _inner;

    public TracingLythonHost(ILythonHost inner)
    {
        _inner = inner;
    }

    public List<string> Trace { get; } = new();

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

    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
    {
        Trace.Add("read-text:" + path);
        return _inner.ReadTextUtf8Async(path, cancellationToken);
    }

    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        Trace.Add("write-text:" + path);
        return _inner.WriteTextUtf8Async(path, utf8, cancellationToken);
    }

    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        Trace.Add("append-text:" + path);
        return _inner.AppendTextUtf8Async(path, utf8, cancellationToken);
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        Trace.Add("read:" + path);
        return _inner.ReadBytesAsync(path, cancellationToken);
    }

    public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        Trace.Add("write:" + path);
        return _inner.WriteBytesAsync(path, bytes, cancellationToken);
    }

    public ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        Trace.Add("append-bytes:" + path);
        return _inner.AppendBytesAsync(path, bytes, cancellationToken);
    }

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        Trace.Add("exists:" + path);
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

    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
    {
        Trace.Add("stat:" + path);
        return _inner.StatAsync(path, cancellationToken);
    }
}
