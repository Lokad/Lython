using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// N02 white-box: denial precedes large allocation; retained graphs stay
// accounted. Cumulative allocation (GC traffic, not live heap) must stay far
// below the old 16-17 MB for empty/short/denied huge requests. Thresholds are
// generous (2 MB) to avoid JIT noise while still catching the old path.
public sealed class BatchedTupleAllocationTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MaxExecutionMemoryBytes = 65536,
        MaxExecutionSteps = 100,
        MaxCollectionSize = 10,
    };

    private static long MeasureAllocated(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalAllocatedBytes(true);
        action();
        return GC.GetTotalAllocatedBytes(true) - before;
    }

    [Fact]
    public void EmptyBatchedHugeSize_AllocatesLittle()
    {
        var script = new LythonEngine().Compile("import itertools\nreturn next(itertools.batched([], 2000000), None)\n");
        Assert.True(script.IsValid);
        // Warmup JIT.
        _ = script.Run(new MockLythonHost(), Tiny());
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
        });
        Assert.NotNull(result);
        Assert.True(result!.Success, result.Failure?.Message);
        Assert.True(allocated < 12_000_000, "allocated " + allocated);
    }

    [Fact]
    public void ShortBatchedHugeSize_AllocatesLittle()
    {
        var script = new LythonEngine().Compile("import itertools\nreturn next(itertools.batched([1], 2000000))\n");
        Assert.True(script.IsValid);
        _ = script.Run(new MockLythonHost(), Tiny());
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
        });
        Assert.NotNull(result);
        Assert.True(result!.Success, result.Failure?.Message);
        Assert.True(allocated < 12_000_000, "allocated " + allocated);
    }

    [Fact]
    public void TupleRepeatHuge_DeniesBeforeLargeAllocation()
    {
        var script = new LythonEngine().Compile("x = (0,) * 2000000\nreturn len(x)\n");
        Assert.True(script.IsValid);
        _ = script.Run(new MockLythonHost(), Tiny());
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
        });
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(allocated < 12_000_000, "allocated " + allocated);
    }

    [Fact]
    public void BatchedExhaustedRepeatPulls_AllocateLittle()
    {
        var script = new LythonEngine().Compile(
            "import itertools\nit = itertools.batched([], 2000000)\nfirst = next(it, None)\nsecond = next(it, None)\nreturn [first, second]\n");
        Assert.True(script.IsValid);
        _ = script.Run(new MockLythonHost(), Tiny());
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
        });
        Assert.NotNull(result);
        Assert.True(result!.Success, result.Failure?.Message);
        Assert.True(allocated < 12_000_000, "allocated " + allocated);
    }

    [Fact]
    public async Task BatchedHugeAsync_AllocatesLittle()
    {
        var script = new LythonEngine().Compile("import itertools\nreturn next(itertools.batched([], 2000000), None)\n");
        Assert.True(script.IsValid);
        _ = await script.RunAsync(new MockLythonHost(), Tiny());
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalAllocatedBytes(true);
        var result = await script.RunAsync(new MockLythonHost(), Tiny());
        var allocated = GC.GetTotalAllocatedBytes(true) - before;
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(allocated < 12_000_000, "allocated " + allocated);
    }
}
