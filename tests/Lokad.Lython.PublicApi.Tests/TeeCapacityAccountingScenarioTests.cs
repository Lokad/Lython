using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG13: tee queue backing capacity stays charged independently from queued
/// payload references, so drained retained pairs cannot recycle the budget.
/// </summary>
public sealed class TeeCapacityAccountingScenarioTests
{
    [Fact]
    public async Task DrainedRetainedPairsStayCharged()
    {
        // MG13 probe: 20 retained pairs, each drained unevenly in turn. Item
        // charges release on dequeue, but the backing arrays stay retained.
        var script = new LythonEngine().Compile(
            """
            import itertools
            pairs = []
            for i in range(20):
                a, b = itertools.tee(range(2000))
                for x in a:
                    pass
                for x in b:
                    pass
                pairs.append((a, b))
            return len(pairs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }
    [Fact]
    public async Task AbandonedCloneBacklogStaysCharged()
    {
        // MG13: advancing only one clone queues the whole source into the
        // abandoned clone under per-item and backing charges.
        var script = new LythonEngine().Compile(
            """
            import itertools
            a, b = itertools.tee(range(200000))
            for x in a:
                pass
            return 0
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
    public async Task AbandonedCloneDrainsCorrectlyWhenFunded()
    {
        // MG13: a fully lagging clone still yields every item once funded, so
        // the backlog charges cannot starve legitimate uneven consumption.
        var script = new LythonEngine().Compile(
            """
            import itertools
            a, b = itertools.tee(range(5000))
            left = list(a)
            right = list(b)
            return [len(left), len(right), left == right]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
        var expected = new List<object?> { new BigInteger(5000), new BigInteger(5000), true };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task FailedSourceKeepsQueuedBacklog()
    {
        // MG13: when the source raises mid-iteration, items already fanned out
        // to a lagging clone stay queued and charged instead of being lost
        // with the failed pull.
        var script = new LythonEngine().Compile(
            """
            import itertools
            def f(x):
                if x == 2:
                    raise ValueError("boom")
                return x
            a, b = itertools.tee(map(f, range(100)))
            results = []
            results.append(next(a))
            try:
                for x in a:
                    pass
            except ValueError:
                results.append("caught")
            results.append(next(b))
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
        var expected = new List<object?> { new BigInteger(0), "caught", new BigInteger(0) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

}
