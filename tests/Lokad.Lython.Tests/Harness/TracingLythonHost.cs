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

    public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
    {
        Trace.Add("read-text:" + path);
        var result = await _inner.ReadTextUtf8Async(path, cancellationToken).ConfigureAwait(false);
        Transfers.Add(new HostTransfer("read-text", path, result.Length));
        return result;
    }

    public async ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        Trace.Add("write-text:" + path);
        await _inner.WriteTextUtf8Async(path, utf8, cancellationToken).ConfigureAwait(false);
        Transfers.Add(new HostTransfer("write-text", path, utf8.Length));
    }

    public async ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        Trace.Add("append-text:" + path);
        await _inner.AppendTextUtf8Async(path, utf8, cancellationToken).ConfigureAwait(false);
        Transfers.Add(new HostTransfer("append-text", path, utf8.Length));
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        Trace.Add("read:" + path);
        var result = await _inner.ReadBytesAsync(path, cancellationToken).ConfigureAwait(false);
        Transfers.Add(new HostTransfer("read", path, result.Length));
        return result;
    }

    public async ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        Trace.Add("write:" + path);
        await _inner.WriteBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        Transfers.Add(new HostTransfer("write", path, bytes.Length));
    }

    public async ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        Trace.Add("append-bytes:" + path);
        await _inner.AppendBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        Transfers.Add(new HostTransfer("append-bytes", path, bytes.Length));
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
