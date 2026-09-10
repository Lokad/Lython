using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: date/time member and operator results own their storage instead of
/// escaping. Twenty thousand retained dates must exceed a 1MB budget and
/// twenty thousand retained 9-tuples a 1.5MB budget in both modes; pre-fix
/// both fit in ~0.6MB of backing storage alone.
/// </summary>
public sealed class DateTimeValueResultScenarioTests
{
    private const long ValueBudgetBytes = 1048576;
    private const long TupleBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedDateArithmeticStaysCharged()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            d = datetime.date(2020, 1, 2)
            t = datetime.timedelta(days=1)
            objs = []
            i = 0
            while i < 20000:
                objs.append(d + t)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ValueBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ValueBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ValueBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedTimetuplesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            d = datetime.date(2020, 1, 2)
            objs = []
            i = 0
            while i < 20000:
                objs.append(d.timetuple())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = TupleBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= TupleBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= TupleBudgetBytes);
    }

    [Fact]
    public async Task DateTimeValueResultsBehave()
    {
        var script = new LythonEngine().Compile("""
            import datetime
            d = datetime.date(2020, 1, 2)
            t = datetime.timedelta(days=1, seconds=2)
            dt = datetime.datetime(2020, 1, 2, 3, 4, 5)
            ta = datetime.time(1, 2, 3, tzinfo=datetime.timezone.utc)
            tz = datetime.timezone(datetime.timedelta(hours=2), "EET")
            dta = datetime.datetime(2020, 1, 2, 3, 4, 5, tzinfo=tz)
            return [d.replace(year=2021).isoformat(), (d + t).isoformat(), str(datetime.date(2020, 1, 3) - d), str(-t), (t * 2).total_seconds(), (t / 2).total_seconds(), t // datetime.timedelta(hours=1), (t % datetime.timedelta(hours=7)).total_seconds(), dt.date().isoformat(), dt.time().isoformat(), dt.timetz().isoformat(), list(d.isocalendar()), list(d.timetuple()), ta.utcoffset().total_seconds(), tz.utcoffset(None).total_seconds(), dta.astimezone(datetime.timezone.utc).isoformat(), dt.replace(hour=9).isoformat(), ta.replace(hour=9).isoformat()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "2021-01-02", "2020-01-03", "1 day, 0:00:00", "-2 days, 23:59:58",
            172804.0, 43201.0, new BigInteger(24), 10802.0,
            "2020-01-02", "03:04:05", "03:04:05",
            new List<object?> { new BigInteger(2020), new BigInteger(1), new BigInteger(4) },
            new List<object?> { new BigInteger(2020), new BigInteger(1), new BigInteger(2), BigInteger.Zero, BigInteger.Zero, BigInteger.Zero, new BigInteger(3), new BigInteger(2), new BigInteger(-1) },
            0.0, 7200.0, "2020-01-02T01:04:05+00:00", "2020-01-02T09:04:05", "09:02:03+00:00",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}