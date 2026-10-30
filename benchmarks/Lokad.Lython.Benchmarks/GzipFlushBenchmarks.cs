using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Flush-scaling benchmarks for contained gzip writes: a fixed 100 KiB
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
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        _noFlush = Compile(engine, WriteSource(100, 0));
        _tenFlushes = Compile(engine, WriteSource(100, 10));
        _hundredFlushes = Compile(engine, WriteSource(100, 100));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _noFlush?.Dispose();
        _tenFlushes?.Dispose();
        _hundredFlushes?.Dispose();
    }

    [Benchmark(Description = "Write 100 KiB gzip without flushes")]
    public void WriteWithoutFlushes() => Run(_noFlush);

    [Benchmark(Description = "Write 100 KiB gzip with 10 flushes")]
    public void WriteWithTenFlushes() => Run(_tenFlushes);

    [Benchmark(Description = "Write 100 KiB gzip with 100 flushes")]
    public void WriteWithHundredFlushes() => Run(_hundredFlushes);

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
        => BenchmarkScripts.Compile(engine, source);

    private static void Run(LythonCompiledScript script)
        => BenchmarkScripts.Run(script, new ZipBinaryHost());

}
