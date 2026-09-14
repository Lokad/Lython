using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Entry-count scaling benchmarks for contained ZIP archives: write and
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
        var seed = new ZipBinaryHost();
        var built = _write.Run(seed);
        if (!built.Success)
        {
            throw new InvalidOperationException(built.Failure?.Message ?? "ZIP scaling benchmark seed failed.");
        }

        _sourceArchive = seed.ReadBytes("/a.zip");
    }

    [Benchmark(Description = "Write N fixed-size DEFLATED entries")]
    public object? WriteEntries() => Run(_write, new ZipBinaryHost());

    [Benchmark(Description = "Read N entries and sum expanded bytes")]
    public object? ReadEntries()
    {
        var host = new ZipBinaryHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        return Run(_read, host);
    }

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
        => BenchmarkScripts.Compile(engine, source);

    private static object? Run(LythonCompiledScript script, ZipBinaryHost host)
        => BenchmarkScripts.Run(script, host);

}
