using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: Context.create_decimal values own their storage like decimal.Decimal().
/// Ten thousand retained values must exceed a 512KiB budget in both modes.
/// </summary>
public sealed class DecimalContextValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedContextDecimalsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            ctx = decimal.getcontext()
            ds = []
            i = 0
            while i < 10000:
                ds.append(ctx.create_decimal(i))
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
    public async Task ContextDecimalBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            ctx = decimal.getcontext()
            return [str(ctx.create_decimal(1) + ctx.create_decimal(2)), ctx.create_decimal_from_float(1.5)]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
}
