using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Scaling benchmarks for contained ZIP archives: staged creation, governed
/// reads, append preservation, and contained extraction over 200 mixed
/// STORED/DEFLATED entries. Baselines are recorded on release runs; see
/// <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class ZipArchiveBenchmarks
{
    private readonly LythonCompiledScript _writeMany;
    private readonly LythonCompiledScript _readMany;
    private readonly LythonCompiledScript _appendMany;
    private readonly LythonCompiledScript _extractMany;
    private readonly byte[] _sourceArchive;

    public ZipArchiveBenchmarks()
    {
        var engine = new LythonEngine();
        _writeMany = Compile(engine, WriteManySource);
        _readMany = Compile(engine, ReadManySource);
        _appendMany = Compile(engine, AppendManySource);
        _extractMany = Compile(engine, ExtractManySource);
        var seed = new ZipBenchmarkHost();
        var built = _writeMany.Run(seed);
        if (!built.Success)
        {
            throw new InvalidOperationException(built.Failure?.Message ?? "ZIP benchmark seed failed.");
        }

        _sourceArchive = seed.ReadBytes("/a.zip");
    }

    [Benchmark(Description = "Write 200 mixed STORED/DEFLATED entries")]
    public object? WriteMixedArchive() => Run(_writeMany, new ZipBenchmarkHost());

    [Benchmark(Description = "List and read 200 mixed entries")]
    public object? ReadMixedArchive()
    {
        var host = new ZipBenchmarkHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        return Run(_readMany, host);
    }

    [Benchmark(Description = "Append 20 entries to a 200-entry archive")]
    public object? AppendMixedArchive()
    {
        var host = new ZipBenchmarkHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        return Run(_appendMany, host);
    }

    [Benchmark(Description = "Extract 200 mixed entries")]
    public object? ExtractMixedArchive()
    {
        var host = new ZipBenchmarkHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        return Run(_extractMany, host);
    }

    private const string WriteManySource = """
        import zipfile
        with zipfile.ZipFile("/a.zip", "w", compression=zipfile.ZIP_DEFLATED) as archive:
            for i in range(200):
                archive.writestr("f" + str(i) + ".txt", "x" * (i + 1))
        return 1
        """;

    private const string ReadManySource = """
        import zipfile
        total = 0
        with zipfile.ZipFile("/a.zip") as archive:
            for name in archive.namelist():
                total = total + len(archive.read(name))
        return total
        """;

    private const string AppendManySource = """
        import zipfile
        with zipfile.ZipFile("/a.zip", "a") as archive:
            for i in range(20):
                archive.writestr("n" + str(i) + ".txt", b"new")
        return 1
        """;

    private const string ExtractManySource = """
        import zipfile
        with zipfile.ZipFile("/a.zip") as archive:
            archive.extractall("/out")
        return 1
        """;

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
    {
        var script = engine.Compile(source);
        if (!script.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return script;
    }

    private static object? Run(LythonCompiledScript script, ZipBenchmarkHost host)
    {
        var result = script.Run(host);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }

    private sealed class ZipBenchmarkHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private static readonly DateTimeOffset Timestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _binary = new(StringComparer.Ordinal);
        private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { "/" };

        public string Cwd => "/";

        public bool CompletesSynchronously => true;

        public DateTimeOffset LocalNow => Timestamp.ToOffset(TimeSpan.FromHours(1));

        public DateTimeOffset UtcNow => Timestamp;

        public void SeedBytes(string path, byte[] payload) => _binary[path] = payload;

        public byte[] ReadBytes(string path) => _binary[path];

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
            throw new LythonHostCapabilityUnavailableException("text file I/O");

        public ValueTask<ReadOnlyMemory<byte>> ReadBytesAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<ReadOnlyMemory<byte>>(_binary[path]);
        }

        public ValueTask WriteBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureDirectory(ParentOf(path));
            _binary[path] = bytes.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_binary.ContainsKey(path) || _directories.Contains(path));
        }

        public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> names = Array.Empty<string>();
            return ValueTask.FromResult(names);
        }

        public ValueTask MkDirAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = path.TrimEnd('/');
            if (normalized.Length == 0)
            {
                normalized = "/";
            }

            if (_directories.Contains(normalized) || _binary.ContainsKey(normalized))
            {
                throw new InvalidOperationException($"Path already exists: {normalized}");
            }

            if (!_directories.Contains(ParentOf(normalized)))
            {
                throw new InvalidOperationException($"Parent directory does not exist: {normalized}");
            }

            _directories.Add(normalized);
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _binary.Remove(path);
            return ValueTask.CompletedTask;
        }

        public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _binary[destination] = _binary[source].ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _binary[destination] = _binary[source];
            _binary.Remove(source);
            return ValueTask.CompletedTask;
        }

        public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_binary.TryGetValue(path, out var payload))
            {
                return ValueTask.FromResult(new LythonPathStat(LythonPathKind.File, payload.Length, Timestamp));
            }

            if (_directories.Contains(path.TrimEnd('/')))
            {
                return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Directory, 0, Timestamp));
            }

            return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
        }

        private void EnsureDirectory(string path)
        {
            if (!_directories.Contains(path))
            {
                EnsureDirectory(ParentOf(path));
                _directories.Add(path);
            }
        }

        private static string ParentOf(string path)
        {
            var slash = path.LastIndexOf('/');
            return slash <= 0 ? "/" : path[..slash];
        }
    }
}
