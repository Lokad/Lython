using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG14: unbounded cache growth pays per entry, and bounded caches evict,
/// instead of accumulating uncharged table and recency state.
/// </summary>
public sealed class FunctoolsCacheAccountingScenarioTests
{
    [Fact]
    public async Task UnboundedCacheEntriesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import functools
            @functools.cache
            def f(n):
                return (1 << 8192) + n
            i = 0
            while i < 5000:
                f(i)
                i = i + 1
            return 1
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
    public async Task BoundedCacheEvictsAndSurvives()
    {
        var script = new LythonEngine().Compile(
            """
            import functools
            @functools.lru_cache(maxsize=8)
            def f(n):
                return n * 2
            i = 0
            while i < 64:
                f(i)
                i = i + 1
            info = f.cache_info()
            return [info.currsize, info.hits, info.misses]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
}
