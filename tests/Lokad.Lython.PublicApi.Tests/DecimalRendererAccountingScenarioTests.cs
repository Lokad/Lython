using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: DecimalTuple and Context renderer outputs own their payload. Two thousand five hundred
/// retained renders must exceed a 512KiB budget in both modes.
/// </summary>
public sealed class DecimalRendererAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedRendersStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            c = decimal.Context()
            ds = []
            i = 0
            while i < 2500:
                ds.append(repr(c))
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
    public async Task RendererBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            checks = [repr(decimal.Decimal("1.5").as_tuple()) == "DecimalTuple(sign=0, digits=(1, 5), exponent=-1)"]
            checks.append(repr(decimal.Context()) == "Context(prec=28, rounding='ROUND_HALF_EVEN', Emin=-999999, Emax=999999, capitals=1, clamp=0)")
            return checks
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
