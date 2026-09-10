using System.Text;
using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Six-column DictReader workloads over a seeded host file at the default
/// execution budget: retaining every dictionary (the incident shape) versus
/// stopping after the first row (the incremental shape). Baselines are
/// recorded on release runs; see <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class CsvReaderBenchmarks
{
    private const int RowCount = 20000;

    private readonly LythonCompiledScript _retainAll;
    private readonly LythonCompiledScript _earlyBreak;
    private readonly BenchmarkHost _host = new();

    public CsvReaderBenchmarks()
    {
        var engine = new LythonEngine();
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
        var seed = new StringBuilder();
        seed.Append("c0,c1,c2,c3,c4,c5\n");
        for (var i = 0; i < RowCount; i++)
        {
            seed.Append("field00,field01,field02,field03,field04,field05\n");
        }

        _host.SeedUtf8("/data.csv", seed.ToString());
    }

    [Benchmark(Description = "DictReader retain 20K six-column dicts")]
    public async Task<object?> RetainAllAsync() => await RunAsync(_retainAll).ConfigureAwait(false);

    [Benchmark(Description = "DictReader break after first of 20K rows")]
    public async Task<object?> EarlyBreakAsync() => await RunAsync(_earlyBreak).ConfigureAwait(false);

    private async Task<object?> RunAsync(LythonCompiledScript script)
    {
        var result = await script.RunAsync(_host).ConfigureAwait(false);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
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
}