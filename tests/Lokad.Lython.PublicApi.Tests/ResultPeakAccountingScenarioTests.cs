using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG24: per-run accounted peaks are observable on the result, so hosts can
/// reconcile enforcement with sampled heap/process peaks.
/// </summary>
public sealed class ResultPeakAccountingScenarioTests
{
    [Fact]
    public async Task PeaksGrowWithRetainedAllocations()
    {
        var small = new LythonEngine().Compile("return 0\n");
        Assert.True(small.IsValid);
        var big = new LythonEngine().Compile(
            """
            xs = []
            i = 0
            while i < 2000:
                xs.append([i])
                i = i + 1
            return 0
            """);
        Assert.True(big.IsValid);
        var smallSync = small.Run(new MockLythonHost());
        Assert.True(smallSync.Success, smallSync.Failure?.Message);
        var bigSync = big.Run(new MockLythonHost());
        Assert.True(bigSync.Success, bigSync.Failure?.Message);
        // 2000 retained small-list backings plus the outer list.
        Assert.True(bigSync.PeakExecutionMemoryBytes >= 400000);
        Assert.True(bigSync.PeakExecutionMemoryBytes > smallSync.PeakExecutionMemoryBytes);

        var smallAsync = await small.RunAsync(new MockLythonHost());
        Assert.True(smallAsync.Success, smallAsync.Failure?.Message);
        var bigAsync = await big.RunAsync(new MockLythonHost());
        Assert.True(bigAsync.Success, bigAsync.Failure?.Message);
        Assert.True(bigAsync.PeakExecutionMemoryBytes >= 400000);
        Assert.True(bigAsync.PeakExecutionMemoryBytes > smallAsync.PeakExecutionMemoryBytes);
    }

    [Fact]
    public async Task FailurePeakStaysWithinBudget()
    {
        var script = new LythonEngine().Compile("x = [0] * 100000\nreturn 0\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes > 0);
        Assert.True(sync.PeakExecutionMemoryBytes <= 65536);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes > 0);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= 65536);
    }

    [Fact]
    public async Task ProjectionPeakIsExactForReturnValue()
    {
        var script = new LythonEngine().Compile("return \"x\" * 10000\n");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new string('x', 10000), sync.ReturnValue);
        Assert.Equal(32L + (2L * 10000), sync.PeakProjectionMemoryBytes);
        Assert.True(sync.PeakExecutionMemoryBytes > 0);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new string('x', 10000), asyncResult.ReturnValue);
        Assert.Equal(32L + (2L * 10000), asyncResult.PeakProjectionMemoryBytes);
        Assert.True(asyncResult.PeakExecutionMemoryBytes > 0);
    }
}