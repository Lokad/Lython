namespace Lokad.Lython.Benchmarks;

// Archive fixtures share the actual owned transport with ordinary file benchmarks.
// Directory and timestamp semantics remain local to the archive workloads.
internal sealed class ZipBinaryHost : ILythonHost, ILythonSynchronousHostCapability
{
    private static readonly DateTimeOffset Timestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private readonly BenchmarkHost _files = new();
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { "/" };
    public string Cwd => "/";
    public bool CompletesSynchronously => true;
    public DateTimeOffset LocalNow => Timestamp.ToOffset(TimeSpan.FromHours(1));
    public DateTimeOffset UtcNow => Timestamp;
    public void SeedBytes(string path, byte[] payload) => _files.SeedBytes(path, payload);
    public byte[] ReadBytes(string path) => _files.ReadBytes(path);
    public ValueTask<ILythonByteFile> OpenFileAsync(LythonPathRequest request, LythonFileOpenOptions options,
        ILythonMemoryBudget budget, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var path = request.Path.StartsWith('/') ? request.Path : request.Cwd.TrimEnd('/') + "/" + request.Path;
        if (_directories.Contains(path.TrimEnd('/')) || path == "/")
            throw new LythonHostException(LythonHostFailureKind.IsDirectory, "Is a directory: " + path);
        if (!_directories.Contains(ParentOf(path)))
            throw new LythonHostException(LythonHostFailureKind.NotFound, "Missing parent: " + path);
        return _files.OpenFileAsync(request, options, budget, token);
    }
    public async ValueTask<bool> ExistsAsync(LythonPathRequest path, CancellationToken token)
        => _directories.Contains(BenchmarkHost.ResolvePath(path)) || await _files.ExistsAsync(path, token);
    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<string>>([]);
    }
    public ValueTask MkDirAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var normalized = path.TrimEnd('/');
        if (normalized.Length == 0) normalized = "/";
        if (!_directories.Contains(ParentOf(normalized)))
            throw new LythonHostException(LythonHostFailureKind.NotFound, "Missing parent: " + normalized);
        if (!_directories.Add(normalized))
            throw new LythonHostException(LythonHostFailureKind.AlreadyExists, "Directory exists: " + normalized);
        return ValueTask.CompletedTask;
    }
    public ValueTask RemoveAsync(string path, CancellationToken token) => _files.RemoveAsync(path, token);
    public ValueTask CopyAsync(string source, string destination, CancellationToken token) => _files.CopyAsync(source, destination, token);
    public ValueTask MoveAsync(string source, string destination, CancellationToken token) => _files.MoveAsync(source, destination, token);
    public async ValueTask<LythonPathStat> StatAsync(LythonPathRequest path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var resolved = BenchmarkHost.ResolvePath(path);
        if (resolved == "/" || _directories.Contains(resolved.TrimEnd('/')))
            return new(LythonPathKind.Directory, 0, Timestamp);
        var stat = await _files.StatAsync(path, token);
        return new(stat.Kind, stat.Size, stat.Exists ? Timestamp : null);
    }
    private static string ParentOf(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash <= 0 ? "/" : path[..slash];
    }
}
