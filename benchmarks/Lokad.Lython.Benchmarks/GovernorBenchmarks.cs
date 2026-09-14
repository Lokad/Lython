using System.Text;
using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

// Governor-path matrix at a realistic 3 MiB execution budget: bounded CSV
// scans (sync, completed async, genuinely-delayed async, 1x/10x rows) plus
// the slice/set temporary loops from the containment milestones. Every case
// returns its count so measured iterations validate the work, not just the
// clock; accounted peaks ride the deterministic scenario pins, while
// BenchmarkDotNet reports timing, managed allocation and collections.
// Baselines are recorded on release runs; see <c>benchmarks/Baselines.md</c>.
[MemoryDiagnoser]
public class GovernorBenchmarks
{
    private const long ThreeMib = 3145728;

    private static readonly LythonRunOptions BoundedOptions = new() { MaxExecutionMemoryBytes = ThreeMib };

    private readonly BenchmarkHost _host = new();
    private readonly DelayedBenchmarkHost _delayedHost = new();
    private readonly LythonCompiledScript _scan20k;
    private readonly LythonCompiledScript _scan200k;
    private readonly LythonCompiledScript _scanFile20k;
    private readonly LythonCompiledScript _slices10k;
    private readonly LythonCompiledScript _sets100k;

    public GovernorBenchmarks()
    {
        var engine = new LythonEngine();
        _scan20k = BenchmarkScripts.Compile(engine, ScanSource(20000));
        _scan200k = BenchmarkScripts.Compile(engine, ScanSource(200000));
        _scanFile20k = BenchmarkScripts.Compile(engine, "f = open(\"/scan.csv\")\ntotal = 0\nfor line in f:\n    total += 1\nreturn total\n");
        _slices10k = BenchmarkScripts.Compile(engine, "b = 'a' * 1000\nx = ''\nfor i in range(10000):\n    x = b[1:]\nreturn len(x)\n");
        _sets100k = BenchmarkScripts.Compile(engine, "x = None\nfor i in range(100000):\n    x = set()\nreturn 0\n");
        var seed = new StringBuilder();
        for (var i = 0; i < 20000; i++)
        {
            seed.Append("field00,field01,field02,field03,field04,field05\n");
        }

        _host.SeedUtf8("/scan.csv", seed.ToString());
        _delayedHost.SeedUtf8("/scan.csv", seed.ToString());
    }

    [Benchmark(Description = "Bounded scalar CSV scan, 20K rows")]
    public object? BoundedCsvScan20k() => BenchmarkScripts.Run(_scan20k, _host, BoundedOptions);

    [Benchmark(Description = "Bounded scalar CSV scan, 20K rows (async)")]
    public async Task<object?> BoundedCsvScan20kAsync() => await BenchmarkScripts.RunAsync(_scan20k, _host, BoundedOptions).ConfigureAwait(false);

    [Benchmark(Description = "Bounded file line scan, 20K rows (delayed async)")]
    public async Task<object?> BoundedFileScan20kDelayedAsync() => await BenchmarkScripts.RunAsync(_scanFile20k, _delayedHost, BoundedOptions).ConfigureAwait(false);

    [Benchmark(Description = "Bounded scalar CSV scan, 200K rows")]
    public object? BoundedCsvScan200k() => BenchmarkScripts.Run(_scan200k, _host, BoundedOptions);

    [Benchmark(Description = "Dropped string slices, 10K x 1K chars")]
    public object? SliceTemporaries10k() => BenchmarkScripts.Run(_slices10k, _host, BoundedOptions);

    [Benchmark(Description = "Dropped empty sets, 100K")]
    public object? SetTemporaries100k() => BenchmarkScripts.Run(_sets100k, _host, BoundedOptions);

    private static string ScanSource(int rows)
        => "import csv\nr = csv.reader('a,b,c,d,e,f\\n' for i in range(" + rows + "))\ntotal = 0\nfor row in r:\n    total += 1\nreturn total\n";
}