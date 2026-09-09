using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: constructed decimals own their storage like other small constructed
/// values. Ten thousand retained decimals must exceed a 512KiB budget in both
/// modes.
/// </summary>
public sealed class DecimalValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedDecimalsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            ds = []
            i = 0
            while i < 10000:
                ds.append(decimal.Decimal(i))
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
    public async Task DecimalBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            return [str(decimal.Decimal(1) + decimal.Decimal(2)), decimal.Decimal("3.50")]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
}