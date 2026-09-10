using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: rendered date/time text (ctime, isoformat, __format__, strftime,
/// tzname) is owned at construction instead of escaping. Twenty thousand
/// retained renders must exceed a 1.5MB budget in both modes; pre-fix they
/// fit in ~0.6MB of backing storage alone.
/// </summary>
public sealed class DateTimeTextAccountingScenarioTests
{
    private const long RenderBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedRendersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            d = datetime.date(2020, 1, 2)
            objs = []
            i = 0
            while i < 20000:
                objs.append(d.strftime("%Y-%m-%d"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RenderBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RenderBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RenderBudgetBytes);
    }

    [Fact]
    public async Task DateTimeTextBehaves()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            d = datetime.date(2020, 1, 2)
            t = datetime.time(1, 2, 3, tzinfo=datetime.timezone.utc)
            dt = datetime.datetime(2020, 1, 2, 3, 4, 5, tzinfo=datetime.timezone.utc)
            tz = datetime.timezone(datetime.timedelta(hours=2), "EET")
            return [d.isoformat(), d.ctime(), d.strftime("%Y/%m/%d"), format(d, "%Y"), t.isoformat(), t.strftime("%H"), format(t, "%M"), t.tzname(), dt.isoformat(), dt.ctime(), dt.strftime("%Y"), format(dt, "%d"), dt.tzname(), tz.tzname(None)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "2020-01-02", "Thu Jan  2 00:00:00 2020", "2020/01/02", "2020",
            "01:02:03+00:00", "01", "02", "UTC",
            "2020-01-02T03:04:05+00:00", "Thu Jan  2 03:04:05 2020", "2020", "02", "UTC",
            "EET",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}