using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: constructed iterator objects own their storage, so retaining many
/// live iterators cannot bypass the execution memory budget. A hoisted source
/// isolates iterator growth from per-iteration literal costs.
/// </summary>
public sealed class IteratorValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedIteratorsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            xs = [1]
            its = []
            i = 0
            while i < 10000:
                its.append(map(abs, xs))
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
    public async Task IteratorBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            return [list(map(abs, [-1])), list(filter(None, [0, 1])), list(zip([1], [2])), list(enumerate("ab"))]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new List<object?> { new BigInteger(1) }, new List<object?> { new BigInteger(1) }, new List<object?> { new List<object?> { new BigInteger(1), new BigInteger(2) } }, new List<object?> { new List<object?> { new BigInteger(0), "a" }, new List<object?> { new BigInteger(1), "b" } } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}