using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: typing.NamedTuple instances own their backing storage, so retaining
/// many small instances cannot bypass the execution memory budget.
/// </summary>
public sealed class TypingNamedTupleAccountingScenarioTests
{
    [Fact]
    public async Task ManyTypingNamedTupleInstancesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            from typing import NamedTuple
            P = NamedTuple("P", [("x", int), ("y", int)])
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
    public async Task TypingNamedTupleConstructionStillProjects()
    {
        var script = new LythonEngine().Compile(
            """
            from typing import NamedTuple
            P = NamedTuple("P", [("x", int), ("y", int)])
            p = P(1, 2)
            q = p._replace(x=9)
            return [p.x, q.x, q.y]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(9), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}