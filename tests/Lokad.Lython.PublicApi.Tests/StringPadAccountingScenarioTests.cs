using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG07: padding expansions meet the budget before their buffers are built,
/// instead of materializing first and charging after.
/// </summary>
public sealed class StringPadAccountingScenarioTests
{
    [Fact]
    public async Task HugeZfillWidthIsBounded()
    {
        var script = new LythonEngine().Compile(
            """
            return "1".zfill(2000000)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }
}
