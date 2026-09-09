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
}
