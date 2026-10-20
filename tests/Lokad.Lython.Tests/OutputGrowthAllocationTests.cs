using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Process allocation includes execution workers. Warmup needs no collection;
// the 2 MB ceiling detects request-sized buffers in these denied operations.
[Collection("AllocationSensitive")]
public sealed class OutputGrowthAllocationTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MemoryBudget = new LythonMemoryBudget(65536),
    };

    private static long MeasureAllocated(System.Func<LythonExecutionResult> run)
    {
        var before = GC.GetTotalAllocatedBytes(true);
        var result = run();
        _ = result;
        return GC.GetTotalAllocatedBytes(true) - before;
    }

    [Fact]
    public void HugeQuantiles_DeniesBeforeLargeAllocation()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import statistics\nreturn len(statistics.quantiles([1, 2], n=200000))\n");
        Assert.True(script.IsValid);
        script.Run(new MockLythonHost(), Tiny()).Dispose();
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
            return result;
        });
        using var owner = result;
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(allocated < 2_000_000, "allocated " + allocated);
    }

    [Fact]
    public void HugeNormalDistSamples_DeniesBeforeLargeAllocation()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import statistics\nreturn len(statistics.NormalDist().samples(200000))\n");
        Assert.True(script.IsValid);
        script.Run(new MockLythonHost(), Tiny()).Dispose();
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
            return result;
        });
        using var owner = result;
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(allocated < 2_000_000, "allocated " + allocated);
    }

    [Fact]
    public void HugeJoin_DeniesBeforeLargeAllocation()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import shlex, itertools\nreturn len(shlex.join(itertools.repeat(\"a\" * 10000, 1000)))\n");
        Assert.True(script.IsValid);
        script.Run(new MockLythonHost(), Tiny()).Dispose();
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
            return result;
        });
        using var owner = result;
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(allocated < 2_000_000, "allocated " + allocated);
    }
}
