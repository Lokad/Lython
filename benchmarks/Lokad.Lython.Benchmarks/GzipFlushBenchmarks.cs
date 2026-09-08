using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Flush-scaling benchmarks for contained gzip writes (R16): a fixed 100 KiB
/// payload written with 0, 10, and 100 explicit flushes. Repeated dirty
/// flushes recompress growing prefixes, so scaling here guards that cost.
/// </summary>
[MemoryDiagnoser]
public class GzipFlushBenchmarks
{
    private readonly LythonCompiledScript _noFlush;
    private readonly LythonCompiledScript _tenFlushes;
    private readonly LythonCompiledScript _hundredFlushes;

    public GzipFlushBenchmarks()
    {
        var engine = new LythonEngine();
        _noFlush = Compile(engine, WriteSource(100, 0));
        _tenFlushes = Compile(engine, WriteSource(100, 10));
        _hundredFlushes = Compile(engine, WriteSource(100, 100));
    }

    [Benchmark(Description = "Write 100 KiB gzip without flushes")]
    public object? WriteWithoutFlushes() => Run(_noFlush);

    [Benchmark(Description = "Write 100 KiB gzip with 10 flushes")]
    public object? WriteWithTenFlushes() => Run(_tenFlushes);

    [Benchmark(Description = "Write 100 KiB gzip with 100 flushes")]
    public object? WriteWithHundredFlushes() => Run(_hundredFlushes);

    private static string WriteSource(int chunks, int flushEvery)
    {
        var script = new System.Text.StringBuilder();
        script.Append("import gzip\nwith gzip.open(\"/o.gz\", \"wb\") as handle:\n");
        for (var chunk = 0; chunk < chunks; chunk++)
        {
            script.Append("    for repetition in range(64):\n");
            script.Append("        handle.write(b\"0123456789abcdef\")\n");
            if (flushEvery > 0 && (chunk + 1) % (chunks / flushEvery) == 0)
            {
                script.Append("    handle.flush()\n");
            }
        }

        script.Append("return 1\n");
        return script.ToString();
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

    private static object? Run(LythonCompiledScript script)
    {
        var result = script.Run(new GzipBenchmarkHost());
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }

    private sealed class GzipBenchmarkHost : ILythonHost, ILythonSynchronousHostCapability
    {
        private static readonly DateTimeOffset Timestamp = new(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
        private readonly Dictionary<string, byte[]> _binary = new(StringComparer.Ordinal);

        public string Cwd => "/";

        public bool CompletesSynchronously => true;

        public DateTimeOffset LocalNow => Timestamp;

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
            _binary[path] = bytes.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(_binary.ContainsKey(path));
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

            return ValueTask.FromResult(new LythonPathStat(LythonPathKind.Missing, 0, null));
        }
    }
}
