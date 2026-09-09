using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: generator-expression objects own their storage like other iterator
/// objects. A hoisted source isolates generator growth from per-iteration
/// literal costs.
/// </summary>
public sealed class GeneratorValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedGeneratorsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            xs = [1]
            gs = []
            i = 0
            while i < 10000:
                gs.append(x for x in xs)
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
    public async Task GeneratorBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile("return list(x * 2 for x in [1, 2, 3])\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2), new BigInteger(4), new BigInteger(6) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}