using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

// Call and integer-loop overhead matrix: positional, keyword, ternary, variadic,
// closure and method calls (plus the 100K integer loop and one async shape).
// BenchmarkDotNet reports timing, managed allocation and collections;
// first-chance exception counts ride one-off observer runs (see the review
// notes), not committed thresholds. Every script result is validated once
// at construction, outside timed sections.
[MemoryDiagnoser]
public class CallOverheadBenchmarks
{
    private readonly BenchmarkHost _host = new();
    private readonly LythonCompiledScript _identity10k;
    private readonly LythonCompiledScript _closure10k;
    private readonly LythonCompiledScript _method10k;
    private readonly LythonCompiledScript _keyword10k;
    private readonly LythonCompiledScript _ternary10k;
    private readonly LythonCompiledScript _variadic10k;
    private readonly LythonCompiledScript _intLoop100k;

    public CallOverheadBenchmarks()
    {
        var engine = new LythonEngine();
        _identity10k = BenchmarkScripts.Compile(engine, "def f(a):\n    return a\nx = 0\nfor i in range(10000):\n    x = f(i)\nreturn x\n");
        _keyword10k = BenchmarkScripts.Compile(engine, "def f(a, b=1):\n    return a + b\nx = 0\nfor i in range(10000):\n    x = f(i, b=2)\nreturn x\n");
        _ternary10k = BenchmarkScripts.Compile(engine, "def f(a, b, c):\n    return a + b + c\nx = 0\nfor i in range(10000):\n    x = f(i, i, i)\nreturn x\n");
        _variadic10k = BenchmarkScripts.Compile(engine, "def f(*a):\n    return len(a)\nx = 0\nfor i in range(10000):\n    x = f(i)\nreturn x\n");
        _intLoop100k = BenchmarkScripts.Compile(engine, "x = 0\nfor i in range(100000):\n    x = x + 1\nreturn x\n");
        _closure10k = BenchmarkScripts.Compile(engine, "def outer():\n    x = 1\n    def inner(a):\n        return a + x\n    return inner\nf = outer()\nx = 0\nfor i in range(10000):\n    x = f(i)\nreturn x\n");
        _method10k = BenchmarkScripts.Compile(engine, "class C:\n    def m(self, a):\n        return a + 1\nc = C()\nx = 0\nfor i in range(10000):\n    x = c.m(i)\nreturn x\n");
        CheckCallOverhead(_identity10k, new BigInteger(9999), "identity10k");
        CheckCallOverhead(_identity10k, new BigInteger(9999), "identity10k-async", async: true);
        CheckCallOverhead(_keyword10k, new BigInteger(10001), "keyword10k");
        CheckCallOverhead(_ternary10k, new BigInteger(29997), "ternary10k");
        CheckCallOverhead(_variadic10k, new BigInteger(1), "variadic10k");
        CheckCallOverhead(_closure10k, new BigInteger(10000), "closure10k");
        CheckCallOverhead(_method10k, new BigInteger(10000), "method10k");
        CheckCallOverhead(_intLoop100k, new BigInteger(100000), "intLoop100k");
    }

    // Setup-only validation (outside timed sections): each script runs once
    // here so a wrong result fails the run before any timing starts.
    private void CheckCallOverhead(LythonCompiledScript script, object expected, string name, bool async = false)
    {
        var actual = async
            ? BenchmarkScripts.RunAsync(script, _host).GetAwaiter().GetResult()
            : BenchmarkScripts.Run(script, _host);
        BenchmarkScripts.CheckResult(actual, expected, name);
    }

    [Benchmark(Description = "10K positional identity calls")]
    public object? CallsIdentity10k() => BenchmarkScripts.Run(_identity10k, _host);

    [Benchmark(Description = "10K positional identity calls (async)")]
    public async Task<object?> CallsIdentity10kAsync() => await BenchmarkScripts.RunAsync(_identity10k, _host).ConfigureAwait(false);

    [Benchmark(Description = "10K keyword identity calls")]
    public object? CallsKeyword10k() => BenchmarkScripts.Run(_keyword10k, _host);

    [Benchmark(Description = "10K ternary positional calls")]
    public object? CallsTernary10k() => BenchmarkScripts.Run(_ternary10k, _host);

    [Benchmark(Description = "10K variadic identity calls")]
    public object? CallsVariadic10k() => BenchmarkScripts.Run(_variadic10k, _host);

    [Benchmark(Description = "10K closure calls")]
    public object? CallsClosure10k() => BenchmarkScripts.Run(_closure10k, _host);

    [Benchmark(Description = "10K method calls")]
    public object? CallsMethod10k() => BenchmarkScripts.Run(_method10k, _host);

    [Benchmark(Description = "100K-step integer loop")]
    public object? IntLoop100k() => BenchmarkScripts.Run(_intLoop100k, _host);
}