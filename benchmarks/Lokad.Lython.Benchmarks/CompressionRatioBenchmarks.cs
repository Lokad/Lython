using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Compression-ratio benchmarks for contained ZIP writes (R30): a single
/// 1,000,000-byte entry that is either maximally compressible (repeated byte)
/// or incompressible (seeded PRNG bytes), both written STORED-blind as
/// DEFLATED. Payloads are built in-script from supported primitives
/// (seeded <c>random.randbytes</c>, bounded string repetition plus
/// <c>encode</c>); binary file reads stay out of the measured region because
/// binary <c>open</c> modes are an intentional Lython divergence. Baselines
/// are recorded on release runs; see <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class CompressionRatioBenchmarks
{
    private readonly LythonCompiledScript _zeros;
    private readonly LythonCompiledScript _random;

    public CompressionRatioBenchmarks()
    {
        var engine = new LythonEngine();
        _zeros = Compile(
            engine,
            "import random\n"
            + "import zipfile\n"
            + "random.seed(42)\n"
            + "data = (\"v\" * 500000 + \"v\" * 500000).encode()\n"
            + "with zipfile.ZipFile(\"/a.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n"
            + "    archive.writestr(\"zeros.bin\", data)\n"
            + "return len(data)\n");
        _random = Compile(
            engine,
            "import random\n"
            + "import zipfile\n"
            + "random.seed(42)\n"
            + "data = random.randbytes(1000000)\n"
            + "with zipfile.ZipFile(\"/a.zip\", \"w\", compression=zipfile.ZIP_DEFLATED) as archive:\n"
            + "    archive.writestr(\"random.bin\", data)\n"
            + "return len(data)\n");
    }

    [Benchmark(Description = "Write 1MB maximally compressible entry")]
    public object? WriteCompressibleEntry() => Run(_zeros, new RatioBenchmarkHost());

    [Benchmark(Description = "Write 1MB incompressible entry")]
    public object? WriteIncompressibleEntry() => Run(_random, new RatioBenchmarkHost());

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
    {
        var script = engine.Compile(source);
        if (!script.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return script;
    }

    private static object? Run(LythonCompiledScript script, RatioBenchmarkHost host)
    {
        var result = script.Run(host);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }

    private sealed class RatioBenchmarkHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private static readonly DateTimeOffset Timestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _binary = new(StringComparer.Ordinal);
        private readonly HashSet<string> _directories = new(StringComparer.Ordinal) { "/" };

        public string Cwd => "/";

        public bool CompletesSynchronously => true;

        public DateTimeOffset LocalNow => Timestamp.ToOffset(TimeSpan.FromHours(1));

        public DateTimeOffset UtcNow => Timestamp;

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
