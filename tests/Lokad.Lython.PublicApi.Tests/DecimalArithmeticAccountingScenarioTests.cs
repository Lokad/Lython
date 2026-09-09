using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: decimal operator results own their storage like constructed decimals.
/// Retained arithmetic results must exceed a 512KiB budget in both modes.
/// </summary>
public sealed class DecimalArithmeticAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedArithmeticResultsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Decimal(7)
            b = decimal.Decimal(2)
            ds = []
            i = 0
            while i < 2000:
                ds.append(a + b)
                ds.append(a - b)
                ds.append(a * b)
                ds.append(a / b)
                ds.append(a % b)
                ds.append(a ** 2)
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
    public async Task ArithmeticBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Decimal(7)
            b = decimal.Decimal(2)
            checks = [(a + b) == decimal.Decimal(9), (a - b) == decimal.Decimal(5)]
            checks.append((a * b) == decimal.Decimal(14))
            checks.append((a / b) == decimal.Decimal("3.5"))
            checks.append((a % b) == decimal.Decimal(1))
            checks.append((a ** 2) == decimal.Decimal(49))
            checks.append((-a) == decimal.Decimal(-7))
            checks.append(abs(b - a) == decimal.Decimal(5))
            return checks
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true, true, true, true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
