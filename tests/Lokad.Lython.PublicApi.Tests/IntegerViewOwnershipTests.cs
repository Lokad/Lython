using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerViewOwnershipTests
{
    [Theory]
    [InlineData("real", false)]
    [InlineData("real", true)]
    [InlineData("numerator", false)]
    [InlineData("numerator", true)]
    [InlineData("conjugate()", false)]
    [InlineData("conjugate()", true)]
    [InlineData("as_integer_ratio()[0]", false)]
    [InlineData("as_integer_ratio()[0]", true)]
    public async Task ExactIntegerViewsPreservePythonIdentity(string view, bool asyncMode)
    {
        var script = new LythonEngine().Compile("x = 1 << 4000\nreturn x." + view + " is x\n");
        Assert.True(script.IsValid);
        var result = asyncMode ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(Assert.IsType<bool>(result.ReturnValue));
    }

    [Theory]
    [InlineData("real", false)]
    [InlineData("real", true)]
    [InlineData("numerator", false)]
    [InlineData("numerator", true)]
    [InlineData("conjugate()", false)]
    [InlineData("conjugate()", true)]
    [InlineData("as_integer_ratio()[0]", false)]
    [InlineData("as_integer_ratio()[0]", true)]
    public async Task RetainedViewsKeepTheirPayloadFunded(string view, bool asyncMode)
    {
        var script = new LythonEngine().Compile("xs = [(1 << 4000)." + view + " for _ in range(2000)]\nreturn len(xs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
        var result = asyncMode ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);
    }
}
