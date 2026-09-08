using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Entry-count scaling benchmarks for contained ZIP archives (R30): write and
/// read fixed-size DEFLATED entries at 50/200/800 entries so archive cost can
/// be related to entry count versus expanded bytes. The read benchmark returns
/// the total expanded bytes, keeping that figure next to the allocation
/// column. Baselines are recorded on release runs; see
/// <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class ZipEntryScalingBenchmarks
{
    [Params(50, 200, 800)]
    public int EntryCount { get; set; }

    private LythonCompiledScript _write = null!;
    private LythonCompiledScript _read = null!;
    private byte[] _sourceArchive = null!;

    [GlobalSetup]
    public void Setup()
    {
        var engine = new LythonEngine();
        _write = Compile(
            engine,
            "import zipfile\n"
            + "with zipfile.ZipFile(\"/a.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n"
            + $"    for i in range({EntryCount}):\n"
            + "        archive.writestr(\"f\" + str(i) + \".txt\", \"v\" * 256)\n"
            + "return 1\n");
        _read = Compile(
            engine,
            "import zipfile\n"
            + "total = 0\n"
            + "with zipfile.ZipFile(\"/a.zip\") as archive:\n"
            + "    for name in archive.namelist():\n"
            + "        total = total + len(archive.read(name))\n"
            + "return total\n");
        var seed = new ZipScalingHost();
        var built = _write.Run(seed);
        if (!built.Success)
        {
            throw new InvalidOperationException(built.Failure?.Message ?? "ZIP scaling benchmark seed failed.");
        }

        _sourceArchive = seed.ReadBytes("/a.zip");
    }

    [Benchmark(Description = "Write N fixed-size DEFLATED entries")]
    public object? WriteEntries() => Run(_write, new ZipScalingHost());

    [Benchmark(Description = "Read N entries and sum expanded bytes")]
    public object? ReadEntries()
    {
        var host = new ZipScalingHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        return Run(_read, host);
    }

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
    {
        var script = engine.Compile(source);
        if (!script.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return script;
    }

    private static object? Run(LythonCompiledScript script, ZipScalingHost host)
    {
        var result = script.Run(host);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }

    private sealed class ZipScalingHost : ILythonHost, ILythonSynchronousHostCapability
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

            if (!_directories.Contains(normalized))
            {
                _directories.Add(normalized);
            }

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
