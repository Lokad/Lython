using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: constructed date/time values own their storage like other small
/// constructed values. Ten thousand retained datetimes must exceed a 384KiB
/// budget in both modes.
/// </summary>
public sealed class DateTimeValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedDatetimesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import datetime
            ds = []
            i = 0
            while i < 10000:
                ds.append(datetime.datetime(2024, 1, 1))
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 393216 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DateTimeBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import datetime
            d = datetime.date(2024, 2, 29)
            t = datetime.time(1, 2, 3)
            return [d.year, t.hour]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2024), new BigInteger(1) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}