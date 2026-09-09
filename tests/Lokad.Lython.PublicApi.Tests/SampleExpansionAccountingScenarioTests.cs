using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG15: counted sampling expansion is transient scratch bounded by a temporary
/// reservation, not uncharged retained growth.
/// </summary>
public sealed class SampleExpansionAccountingScenarioTests
{
    [Fact]
    public async Task CountedExpansionStaysBounded()
    {
        var script = new LythonEngine().Compile(
            """
            import random
            return random.sample(["x"], k=1, counts=[1000000])
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
