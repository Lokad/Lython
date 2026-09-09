using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: Decimal.to_eng_string owns its payload like other scalar renderer
/// outputs. Five thousand retained strings must exceed a 512KiB budget in both
/// modes.
/// </summary>
public sealed class DecimalEngineeringAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedEngineeringStringsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Decimal("1.23456789")
            ds = []
            i = 0
            while i < 5000:
                ds.append(a.to_eng_string())
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
    public async Task EngineeringBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            checks = [decimal.Decimal("1.5").to_eng_string() == "1.5"]
            checks.append(decimal.Decimal("1.5E+3").to_eng_string() == "1.5E+3")
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
