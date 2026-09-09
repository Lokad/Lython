using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: namedtuple instances own their backing storage, so retaining many
/// small instances cannot bypass the execution memory budget.
/// </summary>
public sealed class NamedTupleAccountingScenarioTests
{
    [Fact]
    public async Task ManyNamedtupleInstancesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            from collections import namedtuple
            P = namedtuple("P", ["x", "y"])
            items = []
            i = 0
            while i < 2000:
                items.append(P(i, i + 1))
                i = i + 1
            return len(items)
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
    public async Task NamedTupleConstructionStillProjects()
    {
        var script = new LythonEngine().Compile(
            """
            from collections import namedtuple
            P = namedtuple("P", ["x", "y"])
            p = P(1, 2)
            q = p._replace(x=9)
            r = P._make([3, 4])
            return [p.x, q.x, r[1]]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(9), new BigInteger(4) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}