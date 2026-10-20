using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// Cumulative process allocation sees dedicated execution workers and needs no
// forced collection. Warmup and nonparallel isolation keep the 2 MB threshold
// well below a request-sized backing allocation for two million elements.
[Collection("AllocationSensitive")]
public sealed class BatchedTupleAllocationTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MemoryBudget = new LythonMemoryBudget(65536),
    };

    private static long MeasureAllocated(Action action)
    {
        var before = GC.GetTotalAllocatedBytes(true);
        action();
        return GC.GetTotalAllocatedBytes(true) - before;
    }

    [Fact]
    public void EmptyBatchedHugeSize_AllocatesLittle()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import itertools\nreturn next(itertools.batched([], 2000000), None)\n");
        Assert.True(script.IsValid);
        // Warmup JIT.
        script.Run(new MockLythonHost(), Tiny()).Dispose();
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
        });
        using var resultOwner = result;
        Assert.NotNull(result);
        Assert.True(result!.Success, result.Failure?.Message);
        Assert.True(allocated < 2_000_000, "allocated " + allocated);
    }

    [Fact]
    public void ShortBatchedHugeSize_AllocatesLittle()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import itertools\nreturn next(itertools.batched([1], 2000000))\n");
        Assert.True(script.IsValid);
        script.Run(new MockLythonHost(), Tiny()).Dispose();
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
        });
        using var resultOwner = result;
        Assert.NotNull(result);
        Assert.True(result!.Success, result.Failure?.Message);
        Assert.True(allocated < 2_000_000, "allocated " + allocated);
    }

    [Fact]
    public void TupleRepeatHuge_DeniesBeforeLargeAllocation()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("x = (0,) * 2000000\nreturn len(x)\n");
        Assert.True(script.IsValid);
        script.Run(new MockLythonHost(), Tiny()).Dispose();
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
        });
        using var resultOwner = result;
        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.True(allocated < 2_000_000, "allocated " + allocated);
    }

    [Fact]
    public void BatchedExhaustedRepeatPulls_AllocateLittle()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import itertools\nit = itertools.batched([], 2000000)\nfirst = next(it, None)\nsecond = next(it, None)\nreturn [first, second]\n");
        Assert.True(script.IsValid);
        script.Run(new MockLythonHost(), Tiny()).Dispose();
        LythonExecutionResult? result = null;
        var allocated = MeasureAllocated(() =>
        {
            result = script.Run(new MockLythonHost(), Tiny());
        });
        using var resultOwner = result;
        Assert.NotNull(result);
        Assert.True(result!.Success, result.Failure?.Message);
        Assert.True(allocated < 2_000_000, "allocated " + allocated);
    }

    [Fact]
    public async Task BatchedHugeAsync_AllocatesLittle()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import itertools\nreturn next(itertools.batched([], 2000000), None)\n");
        Assert.True(script.IsValid);
        (await script.RunAsync(new MockLythonHost(), Tiny())).Dispose();
        var before = GC.GetTotalAllocatedBytes(true);
        using var result = await script.RunAsync(new MockLythonHost(), Tiny());
        var allocated = GC.GetTotalAllocatedBytes(true) - before;
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(allocated < 2_000_000, "allocated " + allocated);
    }
}
