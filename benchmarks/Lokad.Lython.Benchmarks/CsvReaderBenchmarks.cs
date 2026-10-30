using System.Text;
using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Six-column DictReader workloads over a seeded host file with the unbounded
/// standalone allowance: retain, discard and stop after the first row. Historical
/// baselines are in <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class CsvReaderBenchmarks
{
    [Params(1000, 10000, 20000, 100000)]
    public int RowCount { get; set; }

    private readonly LythonCompiledScript _retainAll;
    private readonly LythonCompiledScript _earlyBreak;
    private readonly LythonCompiledScript _discardAll;
    private readonly BenchmarkHost _host = new();

    public CsvReaderBenchmarks()
    {
        var engine = new LythonEngine(Utf8RegexProvider.Instance);
        _retainAll = Compile(engine, """
            import csv
            f = open("/data.csv")
            r = csv.DictReader(f)
            out = []
            for row in r:
                out.append(row)
            return len(out)
            """);
        _earlyBreak = Compile(engine, """
            import csv
            f = open("/data.csv")
            r = csv.DictReader(f)
            for row in r:
                return row["c0"]
            return None
            """);
        _discardAll = Compile(engine, """
            import csv
            return sum(1 for row in csv.DictReader(open("/data.csv")))
            """);
    }

    [GlobalSetup]
    public void Seed()
    {
        var seed = new StringBuilder();
        seed.Append("c0,c1,c2,c3,c4,c5\n");
        for (var i = 0; i < RowCount; i++)
        {
            seed.Append("field00,field01,field02,field03,field04,field05\n");
        }

        _host.SeedUtf8("/data.csv", seed.ToString());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _retainAll?.Dispose();
        _earlyBreak?.Dispose();
        _discardAll?.Dispose();
    }

    [Benchmark(Description = "DictReader retain six-column dicts")]
    public async Task RetainAllAsync() => await RunAsync(_retainAll).ConfigureAwait(false);

    [Benchmark(Description = "DictReader break after first row")]
    public async Task EarlyBreakAsync() => await RunAsync(_earlyBreak).ConfigureAwait(false);

    [Benchmark(Description = "DictReader discard rows after counting")]
    public async Task DiscardAllAsync() => await RunAsync(_discardAll).ConfigureAwait(false);

    private Task RunAsync(LythonCompiledScript script)
        => BenchmarkScripts.RunAsync(script, _host);

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
        => BenchmarkScripts.Compile(engine, source);
}
