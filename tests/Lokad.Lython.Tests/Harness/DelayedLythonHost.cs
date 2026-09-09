namespace Lokad.Lython.Tests.Harness;

internal sealed class DelayedLythonHost : ILythonHost
{
    private readonly MockLythonHost _inner;
    private readonly Dictionary<string, TaskCompletionSource> _writePauses = new(StringComparer.Ordinal);

    public DelayedLythonHost()
        : this("/")
    {
    }

    public DelayedLythonHost(string cwd)
    {
        _inner = new MockLythonHost(cwd);
    }

    public int CompletedAsynchronously { get; private set; }

    public string Cwd => _inner.Cwd;

    public DateTimeOffset LocalNow => _inner.LocalNow;

    public DateTimeOffset UtcNow => _inner.UtcNow;

    public ILythonTextInput? StandardInput => _inner.StandardInput;

    public ILythonTextOutput? StandardOutput => _inner.StandardOutput;

    public ILythonTextOutput? StandardError => _inner.StandardError;

    public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;

    public void SeedFile(string path, string text) => _inner.SeedFile(path, text);

    public void SeedBytes(string path, byte[] payload) => _inner.SeedBytes(path, payload);

    public string ReadText(string path) => _inner.ReadText(path);

    public byte[] ReadBytes(string path) => _inner.ReadBytes(path);

    public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        return await _inner.ReadTextUtf8Async(path, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.WriteTextUtf8Async(path, utf8, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.AppendTextUtf8Async(path, utf8, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        return await _inner.ReadBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parks the next write to <paramref name="path"/> until the caller cancels,
    /// returning a task that completes when the parked write starts. Mirrors
    /// the subprocess pause used to prove in-flight cancellation.
    /// </summary>
    public Task PauseWriteUntilCancellation(string path)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _writePauses[path] = started;
        return started.Task;
    }

    public async ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        if (_writePauses.TryGetValue(path, out var paused))
        {
            paused.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.WriteBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.AppendBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        return await _inner.ExistsAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        return await _inner.ListDirAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.MkDirAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.RemoveAsync(path, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.CopyAsync(source, destination, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.MoveAsync(source, destination, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        return await _inner.StatAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private async Task Delay(CancellationToken cancellationToken)
    {
        await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        CompletedAsynchronously++;
    }
}
