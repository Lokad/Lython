using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

// Explicit host batches share one borrowed allowance. Compilation stays funded;
// each execution/result ends before the next batch. These include invocation and
// cleanup costs, without reclamation inside an uninterrupted guest run.
[MemoryDiagnoser]
public class GovernorBenchmarks
{
    private readonly LythonMemoryBudget _budget = new(3 * 1024 * 1024);
    private readonly LythonRunOptions _options;
    private readonly BenchmarkHost _host = new();
    private readonly DelayedBenchmarkHost _delayedHost = new();
    private readonly LythonCompiledScript _csv;
    private readonly LythonCompiledScript _file;
    private readonly LythonCompiledScript _slices;
    private readonly LythonCompiledScript _sets;
    private readonly long _baseline;

    public GovernorBenchmarks()
    {
        var engine = new LythonEngine();
        _options = new LythonRunOptions { MemoryBudget = _budget };
        _csv = Compile("import csv\nreturn sum(1 for row in csv.reader('a,b,c,d,e,f\\n' for i in range(256)))");
        _file = Compile("with open('/scan.csv') as source:\n return sum(1 for line in source)");
        _slices = Compile("b='a'*1000\nfor i in range(64):x=b[1:]\nreturn len(x)");
        _sets = Compile("for i in range(256):x=set()\nreturn 0");
        var seed = string.Concat(Enumerable.Repeat("field00,field01,field02,field03,field04,field05\n", 256));
        _host.SeedUtf8("/scan.csv", seed);
        _delayedHost.SeedUtf8("/scan.csv", seed);
        _baseline = _budget.ReservedBytes;

        LythonCompiledScript Compile(string source)
        {
            var script = engine.Compile(source, _budget);
            if (script.IsValid) return script;
            var message = string.Join(";", script.Diagnostics.Select(d => d.Message));
            script.Dispose();
            throw new InvalidOperationException(message);
        }
    }

    [Benchmark(Description = "CSV: four host batches of 256 records")]
    public void CsvBatches() => RunBatches(_csv, 4, 256);

    [Benchmark(Description = "CSV: four host batches of 256 records (completed async)")]
    public Task CsvBatchesAsync() => RunBatchesAsync(_csv, _host, 256);

    [Benchmark(Description = "File lines: four 256-line executions (delayed async)")]
    public Task FileBatchesDelayedAsync() => RunBatchesAsync(_file, _delayedHost, 256);

    [Benchmark(Description = "CSV: forty host batches of 256 records")]
    public void MoreCsvBatches() => RunBatches(_csv, 40, 256);

    [Benchmark(Description = "Slices: four host batches of 64 slices")]
    public void SliceBatches() => RunBatches(_slices, 4, 999);

    [Benchmark(Description = "Sets: four host batches of 256 empty sets")]
    public void SetBatches() => RunBatches(_sets, 4, 0);

    private void RunBatches(LythonCompiledScript script, int batches, int expected)
    {
        for (var i = 0; i < batches; i++)
        {
            BenchmarkScripts.Run(script, _host, _options, value => BenchmarkScripts.CheckResult(value, new BigInteger(expected), "host batch"));
            CheckBaseline();
        }
    }

    private async Task RunBatchesAsync(LythonCompiledScript script, ILythonHost host, int expected)
    {
        for (var i = 0; i < 4; i++)
        {
            await BenchmarkScripts.RunAsync(script, host, _options, value => BenchmarkScripts.CheckResult(value, new BigInteger(expected), "host batch")).ConfigureAwait(false);
            CheckBaseline();
        }
    }

    private void CheckBaseline()
    {
        if (_budget.ReservedBytes != _baseline) throw new InvalidOperationException("A completed batch retained memory allowance.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _csv.Dispose(); _file.Dispose(); _slices.Dispose(); _sets.Dispose();
        if (_budget.ReservedBytes != 0) throw new InvalidOperationException("Benchmark cleanup retained memory allowance.");
    }
}
