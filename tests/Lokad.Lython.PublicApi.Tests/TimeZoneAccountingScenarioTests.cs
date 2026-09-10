using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: struct_time zone labels own their payload instead of escaping (gmtime
/// shares the fixed label). Twenty thousand retained localtimes must exceed a
/// 6MB budget and twenty thousand retained tzname pairs a 4MB budget in both
/// modes; pre-fix the labels ride invisible.
/// </summary>
public sealed class TimeZoneAccountingScenarioTests
{
    private const long LocaltimeBudgetBytes = 6291456;
    private const long TznameBudgetBytes = 4194304;

    [Fact]
    public async Task ManyRetainedLocaltimesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import time
            objs = []
            i = 0
            while i < 20000:
                objs.append(time.localtime())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = LocaltimeBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= LocaltimeBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= LocaltimeBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedTznamesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import time
            objs = []
            i = 0
            while i < 20000:
                objs.append(time.tzname)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = TznameBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= TznameBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= TznameBudgetBytes);
    }

    [Fact]
    public async Task TimeZonesBehave()
    {
        var script = new LythonEngine().Compile("""
            import time
            g = time.gmtime(0)
            l = time.localtime(0)
            s = time.strptime("2024-01-02 GMT", "%Y-%m-%d %Z")
            return [g.tm_zone, l.tm_zone, list(time.tzname), s.tm_zone]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "GMT", "UTC+01:00", new List<object?> { "UTC+01:00", "UTC+01:00" }, "UTC",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}