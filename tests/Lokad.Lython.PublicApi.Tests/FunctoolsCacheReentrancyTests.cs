using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctoolsCacheReentrancyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearDuringKeyEqualityInvalidatesTheOldHit(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import functools
            enabled=[False]
            @functools.lru_cache(maxsize=2)
            def f(x): return 7
            class E:
                def __hash__(self): return 1
                def __eq__(self,other):
                    if enabled[0]:
                        enabled[0]=False
                        f.cache_clear()
                    return True
            print(f(E()))
            enabled[0]=True
            print(f(E()))
            print(f.cache_info())
            """);
        var budget = new LythonMemoryBudget(65536);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }) : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("7\n7\nCacheInfo(hits=0, misses=1, maxsize=2, currsize=1)\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecursiveMissKeepsTheAlreadyPublishedResultAndDisabledCacheDoesNotHash(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import functools
            entered=[False]
            @functools.lru_cache(maxsize=2)
            def f(x):
                if not entered[0]:
                    entered[0]=True
                    f(x)
                    return 'outer'
                return 'inner'
            print(f(1),f(1))
            @functools.lru_cache(maxsize=0)
            def g(x): return len(x)
            print(g([1,2]))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("outer inner\n2\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeyHashRunsOncePerInvocationAndErrorsRemainCatchable(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import functools
            effects=[]
            class Key:
                def __hash__(self): effects.append('hash'); return 1
                def __eq__(self,other): return True
            @functools.lru_cache(maxsize=1)
            def f(x): return 7
            key=Key()
            print(f(key),f(key),effects)
            class Bad:
                def __hash__(self): raise ValueError('bad hash')
            try: f(Bad())
            except ValueError: print('caught')
            print(f(key),f.cache_info())
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7 7 ['hash', 'hash']\ncaught\n7 CacheInfo(hits=2, misses=1, maxsize=1, currsize=1)\n", result.StandardOutput);
    }
}
