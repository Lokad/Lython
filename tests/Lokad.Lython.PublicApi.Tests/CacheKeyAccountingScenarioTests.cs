using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG14: cache keys own their keyword-name and type-token strings, and dropped
/// lookup keys release nothing net, instead of leaking per call. Twenty
/// thousand distinct kwargs calls must exceed a 12MB budget while twenty
/// thousand cache hits fit in 64KB, in both modes.
/// </summary>
public sealed class CacheKeyAccountingScenarioTests
{
    private const long MissBudgetBytes = 12582912;
    private const long HitBudgetBytes = 65536;

    [Fact]
    public async Task ManyRetainedKeysStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import functools
            @functools.lru_cache(maxsize=None)
            def f(**kw):
                return 1
            i = 0
            while i < 20000:
                f(k=i)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MissBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= MissBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= MissBudgetBytes);
    }

    [Fact]
    public async Task ManyCacheHitsStayFlat()
    {
        var script = new LythonEngine().Compile("""
            import functools
            @functools.lru_cache(maxsize=None)
            def f(**kw):
                return 1
            i = 0
            while i < 20000:
                f(k=1)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = HitBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(sync.PeakExecutionMemoryBytes <= HitBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= HitBudgetBytes);
    }

    [Fact]
    public async Task CacheKeysBehave()
    {
        var script = new LythonEngine().Compile("""
            import functools
            @functools.lru_cache(maxsize=None)
            def f(**kw):
                return kw["k"] * 2
            @functools.lru_cache(maxsize=0)
            def g(n):
                return n + 1
            return [f(k=21), f(k=21), g(1), g(1)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(42), new BigInteger(42), new BigInteger(2), new BigInteger(2),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}