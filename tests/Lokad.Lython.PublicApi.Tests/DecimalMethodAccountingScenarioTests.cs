using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: decimal method results own their storage like operator results.
/// Retained method results must exceed a 512KiB budget in both modes.
/// </summary>
public sealed class DecimalMethodAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedMethodResultsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Decimal("1.55")
            q = decimal.Decimal(1)
            b = decimal.Decimal(2)
            ds = []
            i = 0
            while i < 5000:
                ds.append(a.quantize(q))
                ds.append(b.sqrt())
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
    public async Task MethodBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Decimal("1.51")
            one = decimal.Decimal(1)
            two = decimal.Decimal(2)
            five = decimal.Decimal(5)
            tenth = decimal.Decimal("0.1")
            checks = [a.quantize(tenth) == decimal.Decimal("1.5")]
            checks.append(one.min(two) == one)
            checks.append(two.min(one) == one)
            checks.append(five.rotate(1) == five)
            checks.append(two.copy_abs() == decimal.Decimal(2))
            checks.append(two.sqrt() == two.sqrt())
            checks.append(a.to_integral_value() == decimal.Decimal(2))
            return checks
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true, true, true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
