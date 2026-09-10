using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: kwargs-derived keys in fresh collections own their string payload
/// beside the already-governed backing. Twenty thousand retained counters
/// must exceed a 6MB budget and twenty thousand retained keyword dicts an
/// 8MB budget in both modes; pre-fix the keys ride invisible.
/// </summary>
public sealed class CollectionKwargsKeyAccountingScenarioTests
{
    private const long CounterBudgetBytes = 6291456;
    private const long KeywordsBudgetBytes = 8388608;

    [Fact]
    public async Task ManyRetainedCounterKeysStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import collections
            objs = []
            i = 0
            while i < 20000:
                objs.append(collections.Counter(k=1))
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
    public async Task ManyRetainedPartialKeywordsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import functools
            def f(a, k=0):
                return k
            objs = []
            i = 0
            while i < 20000:
                objs.append(functools.partial(f, k=1).keywords)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = KeywordsBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= KeywordsBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= KeywordsBudgetBytes);
    }

    [Fact]
    public async Task CollectionKwargsKeysBehave()
    {
        var script = new LythonEngine().Compile("""
            import collections
            import functools
            def f(a, k=0):
                return k
            c = collections.Counter(a=2, b=3)
            d = collections.defaultdict(list, a=1)
            o = collections.OrderedDict(a=1)
            p = functools.partial(f, k=7)
            return [c["a"], c["b"], d["a"], list(o.keys()), list(o.values()), p.keywords["k"], p("x")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(2), new BigInteger(3), new BigInteger(1),
            new List<object?> { "a" }, new List<object?> { new BigInteger(1) },
            new BigInteger(7), new BigInteger(7),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}