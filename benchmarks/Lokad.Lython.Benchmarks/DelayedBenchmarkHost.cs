namespace Lokad.Lython.Benchmarks;

// Benchmark host variant whose reads genuinely suspend: every text read
// yields once before delegating, so async runs exercise suspension and
// resumption instead of completing synchronously like BenchmarkHost.
internal sealed class DelayedBenchmarkHost : ILythonHost
{
    private readonly BenchmarkHost _inner = new();

    public string Cwd => _inner.Cwd;

    public DateTimeOffset LocalNow => _inner.LocalNow;

    public DateTimeOffset UtcNow => _inner.UtcNow;

    public void SeedUtf8(string path, string text) => _inner.SeedUtf8(path, text);

    public async ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
        ILythonMemoryBudget budget, CancellationToken token)
        => new DelayedFile(await _inner.OpenFileAsync(request, options, budget, token).ConfigureAwait(false));

    private sealed class DelayedFile(ILythonByteFile file) : ILythonByteFile
    {
        public long Position => file.Position;
        public long? LengthHint => file.LengthHint;
        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            await Task.Yield(); token.ThrowIfCancellationRequested();
            return await file.ReadAsync(buffer, token).ConfigureAwait(false);
        }
        public ValueTask<LythonFileWriteResult> WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token) => file.WriteAsync(buffer, token);
        public ValueTask RewindAndTruncateAsync(CancellationToken token) => file.RewindAndTruncateAsync(token);
        public ValueTask FlushAsync(CancellationToken token) => file.FlushAsync(token);
        public ValueTask DisposeAsync() => file.DisposeAsync();
    }

    public ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken cancellationToken)
        => _inner.ExistsAsync(path, cancellationToken);

    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
        => _inner.ListDirAsync(path, cancellationToken);

    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
        => _inner.MkDirAsync(path, cancellationToken);

    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
        => _inner.RemoveAsync(path, cancellationToken);

    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
        => _inner.CopyAsync(source, destination, cancellationToken);

    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
        => _inner.MoveAsync(source, destination, cancellationToken);

    public ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken cancellationToken)
        => _inner.StatAsync(path, cancellationToken);

    public ReadOnlyMemory<byte> ReadTextUtf8(string path) => _inner.ReadTextUtf8(path);
}
