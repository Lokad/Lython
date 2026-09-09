using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

/// <summary>
/// Async-path benchmarks for large collection materialization:
/// sorting, list materialization with aggregation, and generator consumption
/// executed through <c>RunAsync</c> so the async sequence machinery
/// (<c>ToSequenceAsync</c> and its intermediate buffers) is measured.
/// Baselines are recorded on release runs; see <c>benchmarks/Baselines.md</c>.
/// </summary>
[MemoryDiagnoser]
public class AsyncMaterializationBenchmarks
{
    private readonly LythonCompiledScript _sortLarge;
    private readonly LythonCompiledScript _sumMaterialized;
    private readonly LythonCompiledScript _sumGenerated;

    public AsyncMaterializationBenchmarks()
    {
        var engine = new LythonEngine();
        _sortLarge = Compile(engine, "return sorted(range(20000), reverse=True)");
        _sumMaterialized = Compile(engine, "items = list(range(50000))\nreturn sum(items)");
        _sumGenerated = Compile(engine, "return sum(value * value for value in range(20000))");
    }

    [Benchmark(Description = "Sort 20K integers (async)")]
    public async Task<object?> SortLargeAsync() => await RunAsync(_sortLarge).ConfigureAwait(false);

    [Benchmark(Description = "Sum a materialized 50K list (async)")]
    public async Task<object?> SumMaterializedAsync() => await RunAsync(_sumMaterialized).ConfigureAwait(false);

    [Benchmark(Description = "Sum a 20K generator (async)")]
    public async Task<object?> SumGeneratedAsync() => await RunAsync(_sumGenerated).ConfigureAwait(false);

    private static LythonCompiledScript Compile(LythonEngine engine, string source)
    {
        var script = engine.Compile(source);
        if (!script.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, script.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        return script;
    }

    private static async Task<object?> RunAsync(LythonCompiledScript script)
    {
        var result = await script.RunAsync(new BenchmarkHost()).ConfigureAwait(false);
        return result.Success
            ? result.ReturnValue
            : throw new InvalidOperationException(result.Failure?.Message ?? "Benchmark script failed.");
    }
}
