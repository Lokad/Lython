using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// N04 white-box: output growth denies before large CLR allocation.
// Thresholds generous (20 MB) to absorb Release per-run overhead (~9 MB)
// while still catching old 6.5 MB quantiles/samples and 60 MB join traffic
// (old totals ~15 MB / ~69 MB with overhead).
public sealed class OutputGrowthAllocationTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MaxExecutionMemoryBytes = 65536,
        MaxExecutionSteps = 100,
        MaxCollectionSize = 10,
    };

    private static long MeasureAllocated(System.Func<LythonExecutionResult> run)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalAllocatedBytes(true);
        var result = run();
        _ = result;
        return GC.GetTotalAllocatedBytes(true) - before;
    }

    [Fact]
    public void HugeQuantiles_DeniesBeforeLargeAllocation()
    {
        var script = new LythonEngine().Compile("import statistics\nreturn len(statistics.quantiles([1, 2], n=200000))\n");
        Assert.True(script.IsValid);
        _ = script.Run(new MockLythonHost(), Tiny());
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
            return result;
        });
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(allocated < 20_000_000, "allocated " + allocated);
    }

    [Fact]
    public void HugeNormalDistSamples_DeniesBeforeLargeAllocation()
    {
        var script = new LythonEngine().Compile("import statistics\nreturn len(statistics.NormalDist().samples(200000))\n");
        Assert.True(script.IsValid);
        _ = script.Run(new MockLythonHost(), Tiny());
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
            return result;
        });
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(allocated < 20_000_000, "allocated " + allocated);
    }

    [Fact]
    public void HugeJoin_DeniesBeforeLargeAllocation()
    {
        var script = new LythonEngine().Compile("import shlex, itertools\nreturn len(shlex.join(itertools.repeat(\"a\" * 10000, 1000)))\n");
        Assert.True(script.IsValid);
        _ = script.Run(new MockLythonHost(), Tiny());
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
            return result;
        });
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(allocated < 20_000_000, "allocated " + allocated);
    }
}
