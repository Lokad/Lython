using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG10: deque nodes stay charged for their lifetime, so a large deque cannot
/// bypass the execution memory budget.
/// </summary>
public sealed class DequeAccountingScenarioTests
{
    [Fact]
    public async Task LargeDequeStaysCharged()
    {
        // MG10 probe: 100000 lazy inputs materialize deque nodes without any
        // governor ownership, retaining about 8.9 MiB live heap under 64 KiB.
        var script = new LythonEngine().Compile(
            """
            from collections import deque
            d = deque(range(100000))
            return len(d)
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
    public async Task SmallDequeOperationsSucceedWhenFunded()
    {
        var script = new LythonEngine().Compile(
            """
            from collections import deque
            d = deque([1, 2, 3])
            d.append(4)
            d.appendleft(0)
            left = d.popleft()
            right = d.pop()
            d.reverse()
            return [left, right, list(d)]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
}