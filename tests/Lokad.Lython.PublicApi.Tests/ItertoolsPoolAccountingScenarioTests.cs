using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG12: combinatoric input pools stay owned by their iterators, so lazy inputs
/// cannot materialize outside the memory budget before producing anything.
/// </summary>
public sealed class ItertoolsPoolAccountingScenarioTests
{
    [Fact]
    public async Task ProductPoolStaysCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import itertools
            return itertools.product(range(100000))
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

    [Fact]
    public async Task CombinationsPoolStaysCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import itertools
            return itertools.combinations(range(100000), 2)
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
