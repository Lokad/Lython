using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Compression-ratio benchmarks for contained ZIP writes: a single
/// 1,000,000-byte entry that is either maximally compressible (repeated byte)
/// or incompressible (seeded PRNG bytes), both written STORED-blind as
/// DEFLATED. Payloads are built in-script from supported primitives
/// (seeded <c>random.randbytes</c>, bounded string repetition plus
/// <c>encode</c>); binary file reads stay out of the measured region. Historical
/// baselines are in <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class CompressionRatioBenchmarks
{
    private readonly LythonCompiledScript _zeros;
    private readonly LythonCompiledScript _random;

    public CompressionRatioBenchmarks()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
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

    [GlobalCleanup]
    public void Cleanup()
    {
        _zeros?.Dispose();
        _random?.Dispose();
    }

    [Benchmark(Description = "Write 1MB maximally compressible entry")]
    public void WriteCompressibleEntry() => Run(_zeros, new ZipBinaryHost());

    [Benchmark(Description = "Write 1MB incompressible entry")]
    public void WriteIncompressibleEntry() => Run(_random, new ZipBinaryHost());

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
        => BenchmarkScripts.Compile(engine, source);

    private static void Run(LythonCompiledScript script, ZipBinaryHost host)
        => BenchmarkScripts.Run(script, host);

}
