namespace Lokad.Lython.Tests.Harness;

internal sealed class DelayedLythonHost : ILythonHost
{
    private readonly MockLythonHost _inner;
    private readonly Dictionary<string, TaskCompletionSource> _writePauses = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource> _readPauses = new(StringComparer.Ordinal);

    public DelayedLythonHost()
        : this("/")
    {
    }

    public DelayedLythonHost(string cwd)
    {
        _inner = new MockLythonHost(cwd);
    }

    public int CompletedAsynchronously { get; private set; }
    public int RangedReadCount => _inner.RangedReadCount;

    public Action? OnTextReadCompleted { get; set; }

    public Action? OnStandardOutputWrite
    {
        get => _inner.OnStandardOutputWrite;
        set => _inner.OnStandardOutputWrite = value;
    }

    public string Cwd => _inner.Cwd;

    public DateTimeOffset LocalNow => _inner.LocalNow;

    public DateTimeOffset UtcNow => _inner.UtcNow;

    public ILythonTextInput? StandardInput => _inner.StandardInput;

    public ILythonTextOutput? StandardOutput => _inner.StandardOutput;

    public ILythonTextOutput? StandardError => _inner.StandardError;

    public ILythonSubprocessRunner? SubprocessRunner => _inner.SubprocessRunner;

    public ILythonTiming? Timing => _inner.Timing;

    public void SeedFile(string path, string text) => _inner.SeedFile(path, text);

    public void EnableSubprocess() => _inner.EnableSubprocess();

    public void SeedBytes(string path, byte[] payload) => _inner.SeedBytes(path, payload);

    public string ReadText(string path) => _inner.ReadText(path);

    public void SetTiming(ILythonTiming timing) => _inner.SetTiming(timing);

    public byte[] ReadBytes(string path) => _inner.ReadBytes(path);

    public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
        ILythonMemoryBudget budget, CancellationToken token)
    {
        await Delay(token).ConfigureAwait(false);
        var file = await _inner.OpenFileAsync(request, options, budget, token).ConfigureAwait(false);
        return new DelayedByteFile(this, request.Path.StartsWith('/') ? request.Path : request.Cwd.TrimEnd('/') + "/" + request.Path, file);
    }

    private sealed class DelayedByteFile(DelayedLythonHost host, string path, ILythonByteFile file) : ILythonByteFile
    {
        public long Position => file.Position;
        public long? LengthHint => file.LengthHint;
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (host._readPauses.TryGetValue(path, out var paused))
            {
                paused.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            }
            await host.Delay(token).ConfigureAwait(false);
            var count = await file.ReadAsync(buffer, token).ConfigureAwait(false);
            host.OnTextReadCompleted?.Invoke();
            return count;
        }
        public async ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token)
        {
            await host.PauseWriteIfConfigured(path, buffer.Length, token).ConfigureAwait(false);
            await host.Delay(token).ConfigureAwait(false);
            return await file.WriteAsync(buffer, token).ConfigureAwait(false);
        }
        public async ValueTask FlushAsync(CancellationToken token)
        {
            await host.Delay(token).ConfigureAwait(false);
            await file.FlushAsync(token).ConfigureAwait(false);
        }
        public async ValueTask DisposeAsync()
        {
            try { await host.Delay(CancellationToken.None).ConfigureAwait(false); }
            finally { await file.DisposeAsync().ConfigureAwait(false); }
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
    {
        if (_readPauses.TryGetValue(path, out var paused))
        {
            paused.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        await Delay(cancellationToken).ConfigureAwait(false);
        var bytes = await _inner.ReadTextUtf8Async(path, cancellationToken).ConfigureAwait(false);
        OnTextReadCompleted?.Invoke();
        return bytes;
    }

    public async ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        await PauseWriteIfConfigured(path, utf8.Length, cancellationToken).ConfigureAwait(false);
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.WriteTextUtf8Async(path, utf8, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        await PauseWriteIfConfigured(path, utf8.Length, cancellationToken).ConfigureAwait(false);
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.AppendTextUtf8Async(path, utf8, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
    {
        if (_readPauses.TryGetValue(path, out var paused))
        {
            paused.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }
        await Delay(cancellationToken).ConfigureAwait(false);
        return await _inner.ReadBytesAsync(path, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parks the next nonempty write to <paramref name="path"/> until the caller cancels,
    /// returning a task that completes when the parked write starts. Mirrors
    /// the subprocess pause used to prove in-flight cancellation.
    /// </summary>
    public Task PauseWriteUntilCancellation(string path)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _writePauses[path] = started;
        return started.Task;
    }

    /// <summary>
    /// Parks the next read of <paramref name="path"/> until the caller cancels,
    /// returning a task that completes when the parked read starts. Read-side
    /// mirror of PauseWriteUntilCancellation: chunked file iteration composes
    /// ranged reads over ReadTextUtf8Async, so parking here also parks drains,
    /// constructors and callbacks reached through lazy iteration.
    /// </summary>
    public Task PauseReadUntilCancellation(string path)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _readPauses[path] = started;
        return started.Task;
    }

    public async ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await PauseWriteIfConfigured(path, bytes.Length, cancellationToken).ConfigureAwait(false);
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.WriteBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask PauseWriteIfConfigured(string path, int length, CancellationToken cancellationToken)
    {
        if (length>0 && _writePauses.TryGetValue(path, out var paused))
        {
            paused.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

    }

    public async ValueTask AppendBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await PauseWriteIfConfigured(path, bytes.Length, cancellationToken).ConfigureAwait(false);
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

    public async ValueTask RemoveFileAsync(string path, bool missingOk, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.RemoveFileAsync(path, missingOk, cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask RemoveDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.RemoveDirectoryAsync(path, cancellationToken).ConfigureAwait(false);
    }
    public async ValueTask ReplaceAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await Delay(cancellationToken).ConfigureAwait(false);
        await _inner.ReplaceAsync(source, destination, cancellationToken).ConfigureAwait(false);
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
