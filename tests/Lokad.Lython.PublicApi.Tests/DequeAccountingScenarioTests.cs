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
    [Fact]
    public async Task RetainedEmptyDequesDeny()
    {
        // Shells accumulate per live instance: 20k empty deques retain
        // ~2.5 MiB of shells alone.
        var script = new LythonEngine().Compile(
            """
            from collections import deque
            ds = []
            for i in range(20000):
                ds.append(deque())
            return len(ds)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DroppedPopulatedDequesRelease()
    {
        // Dropped deques (shells and nodes) reclaim through the pool, so a
        // bounded churn loop completes under a small fixed budget.
        var script = new LythonEngine().Compile(
            """
            from collections import deque
            for i in range(20000):
                d = deque([1, 2, 3])
            return 0
            """);
        Assert.True(script.IsValid);
        var expected = new BigInteger(0);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AliasedDequeSurvivesOneDrop()
    {
        // Dropping one alias must not release retention owned through the other.
        var script = new LythonEngine().Compile(
            """
            from collections import deque
            d = deque([1, 2, 3])
            e = d
            d = None
            return len(e)
            """);
        Assert.True(script.IsValid);
        var expected = new BigInteger(3);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ClearedDequeReusesCleanly()
    {
        // Clear releases nodes while the shell persists; regrowth re-charges.
        var script = new LythonEngine().Compile(
            """
            from collections import deque
            d = deque([1] * 1000)
            d.clear()
            for i in range(100):
                d.append(i)
            return len(d)
            """);
        Assert.True(script.IsValid);
        var expected = new BigInteger(100);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
