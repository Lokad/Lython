using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

// Call and integer-loop overhead matrix behind the item-10 profiles: plain,
// keyword and variadic identity calls (sync/async) plus the 100K integer
// loop. BenchmarkDotNet reports timing, managed allocation and collections;
// first-chance exception counts ride one-off observer runs (see the review
// notes), not committed thresholds.
[MemoryDiagnoser]
public class CallOverheadBenchmarks
{
    private readonly BenchmarkHost _host = new();
    private readonly LythonCompiledScript _identity10k;
    private readonly LythonCompiledScript _keyword10k;
    private readonly LythonCompiledScript _variadic10k;
    private readonly LythonCompiledScript _intLoop100k;

    public CallOverheadBenchmarks()
    {
        var engine = new LythonEngine();
        _identity10k = BenchmarkScripts.Compile(engine, "def f(a):\n    return a\nx = 0\nfor i in range(10000):\n    x = f(i)\nreturn x\n");
        _keyword10k = BenchmarkScripts.Compile(engine, "def f(a, b=1):\n    return a + b\nx = 0\nfor i in range(10000):\n    x = f(i, b=2)\nreturn x\n");
        _variadic10k = BenchmarkScripts.Compile(engine, "def f(*a):\n    return len(a)\nx = 0\nfor i in range(10000):\n    x = f(i)\nreturn x\n");
        _intLoop100k = BenchmarkScripts.Compile(engine, "x = 0\nfor i in range(100000):\n    x = x + 1\nreturn x\n");
    }

    [Benchmark(Description = "10K positional identity calls")]
    public object? CallsIdentity10k() => BenchmarkScripts.Run(_identity10k, _host);

    [Benchmark(Description = "10K positional identity calls (async)")]
    public async Task<object?> CallsIdentity10kAsync() => await BenchmarkScripts.RunAsync(_identity10k, _host).ConfigureAwait(false);

    [Benchmark(Description = "10K keyword identity calls")]
    public object? CallsKeyword10k() => BenchmarkScripts.Run(_keyword10k, _host);

    [Benchmark(Description = "10K variadic identity calls")]
    public object? CallsVariadic10k() => BenchmarkScripts.Run(_variadic10k, _host);

    [Benchmark(Description = "100K-step integer loop")]
    public object? IntLoop100k() => BenchmarkScripts.Run(_intLoop100k, _host);
}