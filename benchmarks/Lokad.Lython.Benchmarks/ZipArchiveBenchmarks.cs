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
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        _writeMany = Compile(engine, WriteManySource);
        _readMany = Compile(engine, ReadManySource);
        _appendMany = Compile(engine, AppendManySource);
        _extractMany = Compile(engine, ExtractManySource);
        var seed = new ZipBinaryHost();
        using var built = _writeMany.Run(seed);
        if (!built.Success)
        {
            throw new InvalidOperationException(built.Failure?.Message ?? "ZIP benchmark seed failed.");
        }

        _sourceArchive = seed.ReadBytes("/a.zip");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _writeMany?.Dispose();
        _readMany?.Dispose();
        _appendMany?.Dispose();
        _extractMany?.Dispose();
    }

    [Benchmark(Description = "Write 200 mixed STORED/DEFLATED entries")]
    public void WriteMixedArchive() => Run(_writeMany, new ZipBinaryHost());

    [Benchmark(Description = "List and read 200 mixed entries")]
    public void ReadMixedArchive()
    {
        var host = new ZipBinaryHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        Run(_readMany, host);
    }

    [Benchmark(Description = "Append 20 entries to a 200-entry archive")]
    public void AppendMixedArchive()
    {
        var host = new ZipBinaryHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        Run(_appendMany, host);
    }

    [Benchmark(Description = "Extract 200 mixed entries")]
    public void ExtractMixedArchive()
    {
        var host = new ZipBinaryHost();
        host.SeedBytes("/a.zip", _sourceArchive);
        Run(_extractMany, host);
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
        => BenchmarkScripts.Compile(engine, source);

    private static void Run(LythonCompiledScript script, ZipBinaryHost host)
        => BenchmarkScripts.Run(script, host);

}
