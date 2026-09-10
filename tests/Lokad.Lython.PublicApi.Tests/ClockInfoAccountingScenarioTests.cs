using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: clock-info objects own a shell beside shared implementation labels,
/// so retained results accumulate instead of riding the call budget-free.
/// Twenty thousand retained infos must exceed a 1MB budget in both modes;
/// pre-fix they fit in ~0.6MB of backing storage alone.
/// </summary>
public sealed class ClockInfoAccountingScenarioTests
{
    private const long ClockBudgetBytes = 1048576;

    [Fact]
    public async Task ManyRetainedClockInfosStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import time
            objs = []
            i = 0
            while i < 20000:
                objs.append(time.get_clock_info("time"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ClockBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ClockBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ClockBudgetBytes);
    }

    [Fact]
    public async Task ClockInfosBehave()
    {
        var script = new LythonEngine().Compile("""
            import time
            c = time.get_clock_info("time")
            m = time.get_clock_info("monotonic")
            return [c.adjustable, c.implementation, c.monotonic, m.adjustable, m.implementation, m.monotonic]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, "Lython host UTC wall clock", false,
            false, "Lython host monotonic clock", true,
        };
        var syncHost = new MockLythonHost();
        syncHost.EnableTiming();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncHost = new MockLythonHost();
        asyncHost.EnableTiming();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}