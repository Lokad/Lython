using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: Counter objects own their shell beside the governed inner dict,
/// including copies; counts stay owned by the existing paths.
/// </summary>
public sealed class CounterShellAccountingScenarioTests
{
    // 20k retained counters own a 64B shell on top of the 192B dict base and
    // a 16B slot, so they fit 5MB pre-fix and trip post-fix.
    private const long CounterBudgetBytes = 5000000;

    [Fact]
    public async Task ManyRetainedCountersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import collections
            objs = []
            i = 0
            while i < 20000:
                objs.append(collections.Counter())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = CounterBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= CounterBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= CounterBudgetBytes);
    }

    [Fact]
    public async Task CounterShellsBehave()
    {
        var script = new LythonEngine().Compile("""
            import collections
            c = collections.Counter("aab")
            d = c.copy()
            d["c"] = 4
            return [c["a"], c["b"], c["z"], len(c), d["c"], len(d)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(2), new BigInteger(1), new BigInteger(0), new BigInteger(2), new BigInteger(4), new BigInteger(3) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}