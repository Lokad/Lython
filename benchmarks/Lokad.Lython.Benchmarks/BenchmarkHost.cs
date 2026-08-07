using System.Text;

namespace Lokad.Lython.Benchmarks;

internal sealed class BenchmarkHost : ILythonHost
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public string Cwd => "/";

    public DateTimeOffset LocalNow => new(2024, 1, 2, 4, 4, 5, TimeSpan.FromHours(1));

    public DateTimeOffset UtcNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);

    public void SeedUtf8(string path, string text)
    {
        _files[path] = Encoding.UTF8.GetBytes(text);
    }

    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(_files.TryGetValue(path, out var utf8) ? utf8 : ReadOnlyMemory<byte>.Empty);
    }

    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _files[path] = utf8.ToArray();
        return ValueTask.CompletedTask;
    }

    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_files.TryGetValue(path, out var existing))
        {
            var appended = new byte[existing.Length + utf8.Length];
            existing.CopyTo(appended, 0);
            utf8.Span.CopyTo(appended.AsSpan(existing.Length));
            _files[path] = appended;
            return ValueTask.CompletedTask;
        }

        _files[path] = utf8.ToArray();
        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_files.ContainsKey(path));
    }

    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<string>>(_files.Keys.Where(key => key.StartsWith(path, StringComparison.Ordinal)).ToArray());
    }

    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _files.Remove(path);
        return ValueTask.CompletedTask;
    }

    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _files[destination] = _files[source].ToArray();
        return ValueTask.CompletedTask;
    }

    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _files[destination] = _files[source];
        _files.Remove(source);
        return ValueTask.CompletedTask;
    }

    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_files.TryGetValue(path, out var utf8))
        {
            throw new FileNotFoundException(path);
        }

        return ValueTask.FromResult(new LythonPathStat(
            kind: LythonPathKind.File,
            size: utf8.Length,
            modifiedAt: DateTimeOffset.UnixEpoch));
    }

    public ReadOnlyMemory<byte> ReadTextUtf8(string path)
        => ReadTextUtf8Async(path, CancellationToken.None).GetAwaiter().GetResult();
}
