using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: round() decimal results own their storage like other decimal results.
/// Ten thousand retained rounded values must exceed a 512KiB budget in both modes.
/// </summary>
public sealed class DecimalRoundAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedRoundedResultsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Decimal("1.55")
            ds = []
            i = 0
            while i < 10000:
                ds.append(round(a, 1))
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoundBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Decimal("1.51")
            checks = [round(a) == 2]
            checks.append(round(a, 1) == decimal.Decimal("1.5"))
            checks.append(round(decimal.Decimal(5), 29) == decimal.Decimal(5))
            return checks
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
