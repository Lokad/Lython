using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Member-lookup scaling benchmarks for contained ZIP archives: repeated getinfo
/// and read of the last entry by name at 50/200/800 entries, so the governed
/// last-name-to-ordinal index shows constant-time behavior instead of directory
/// rescans. Baselines are recorded on release runs; see
/// <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class ZipLookupBenchmarks
{
    [Params(50, 200, 800)]
    public int EntryCount { get; set; }

    private LythonCompiledScript _lookup = null!;
    private byte[] _sourceArchive = null!;

    [GlobalSetup]
    public void Setup()
    {
        var engine = new LythonEngine();
        var write = Compile(
            engine,
            "import zipfile\n"
            + "with zipfile.ZipFile(\"/a.zip\", \"w\") as archive:\n"
            + $"    for i in range({EntryCount}):\n"
            + "        archive.writestr(\"f\" + str(i) + \".txt\", \"v\" * 256)\n"
            + "return 1\n");
        var lastName = "f" + (EntryCount - 1) + ".txt";
        _lookup = Compile(
            engine,
            "import zipfile\n"
            + "total = 0\n"
            + "with zipfile.ZipFile(\"/a.zip\") as archive:\n"
            + "    for i in range(200):\n"
            + $"        archive.getinfo(\"{lastName}\")\n"
            + $"        total = total + len(archive.read(\"{lastName}\"))\n"
            + "return total\n");
        var seed = new ZipBinaryHost();
        var built = write.Run(seed);
        if (!built.Success)
        {
            throw new InvalidOperationException(built.Failure?.Message ?? "ZIP lookup benchmark seed failed.");
        }

        _sourceArchive = seed.ReadBytes("/a.zip");
    }

    [Benchmark(Description = "Lookup last entry by name 200 times")]
    public object? LookupLast()
    {
        var host = new ZipBinaryHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        return Run(_lookup, host);
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

    private static object? Run(LythonCompiledScript script, ZipBinaryHost host)
    {
        var result = script.Run(host);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }
}
