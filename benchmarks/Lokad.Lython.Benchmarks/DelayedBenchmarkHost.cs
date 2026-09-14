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

    public async ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        return await _inner.ReadTextUtf8Async(path, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        => _inner.WriteTextUtf8Async(path, utf8, cancellationToken);

    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
        => _inner.AppendTextUtf8Async(path, utf8, cancellationToken);

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
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

    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        => _inner.StatAsync(path, cancellationToken);

    public ReadOnlyMemory<byte> ReadTextUtf8(string path) => _inner.ReadTextUtf8(path);
}