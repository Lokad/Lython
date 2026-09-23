using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace Lokad.Lython.Benchmarks;

// Member-resolution overhead after the N17 signature hoisting: fresh receivers
// force a runtime member-cache miss per iteration (the full BoundCallable.Create
// path, now riding hoisted static signatures), while a stable receiver measures
// the cached path. Every script result is validated once at construction,
// outside timed sections.
[MemoryDiagnoser]
public class MemberResolutionBenchmarks
{
    private readonly BenchmarkHost _host = new();
    private readonly LythonCompiledScript _indexMiss;
    private readonly LythonCompiledScript _indexHit;
    private readonly LythonCompiledScript _countMiss;

    public MemberResolutionBenchmarks()
    {
        var engine = new LythonEngine();
        _indexMiss = BenchmarkScripts.Compile(engine, "total = 0\nfor i in range(2000):\n    total += [i].index(i)\nreturn total\n");
        _indexHit = BenchmarkScripts.Compile(engine, "x = [0]\ntotal = 0\nfor i in range(2000):\n    total += x.index(0)\nreturn total\n");
        _countMiss = BenchmarkScripts.Compile(engine, "total = 0\nfor i in range(2000):\n    total += [i, i + 1].count(i)\nreturn total\n");
        Check(_indexMiss, new BigInteger(0), "index-miss");
        Check(_indexHit, new BigInteger(0), "index-hit");
        Check(_countMiss, new BigInteger(2000), "count-miss");
    }

    private void Check(LythonCompiledScript script, object expected, string name)
        => BenchmarkScripts.CheckResult(BenchmarkScripts.Run(script, _host), expected, name);

    [Benchmark(Description = "2K list.index over fresh receivers (cache miss each)")]
    public object? ListIndexFreshReceiver() => BenchmarkScripts.Run(_indexMiss, _host);

    [Benchmark(Description = "2K list.index over a stable 1-list (cache hit)")]
    public object? ListIndexStableReceiver() => BenchmarkScripts.Run(_indexHit, _host);

    [Benchmark(Description = "2K list.count over fresh receivers (cache miss each)")]
    public object? ListCountFreshReceiver() => BenchmarkScripts.Run(_countMiss, _host);
}
