using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: every itertools iterator object owns its constructed-value unit like
/// the builtin lazy iterables, on top of any governed pools, queues, index
/// arrays or yielded values the family already charges.
/// </summary>
public sealed class ItertoolsValueAccountingScenarioTests
{
    // 20k retained iterators own 128B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix (input and pools stay shared and small) and trip post-fix.
    private const long IteratorBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedRepeatIteratorsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import itertools
            objs = []
            i = 0
            while i < 20000:
                objs.append(itertools.repeat(i))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = IteratorBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= IteratorBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= IteratorBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedChainIteratorsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import itertools
            src = [1]
            objs = []
            i = 0
            while i < 20000:
                objs.append(itertools.chain(src))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = IteratorBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ItertoolsValuesBehave()
    {
        var script = new LythonEngine().Compile("""
            import itertools
            a = list(itertools.islice(itertools.count(10), 3))
            b = list(itertools.repeat("x", 2))
            c = list(itertools.chain([1], [2, 3]))
            d = list(itertools.zip_longest([1, 2], ["a"], fillvalue="?"))
            t = itertools.tee([1, 2], 3)
            g = itertools.groupby([1, 1, 2])
            return [a, b, c, d[0][0], d[1][1], len(t), len(list(g))]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new List<object?> { new BigInteger(10), new BigInteger(11), new BigInteger(12) }, new List<object?> { "x", "x" }, new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(3) }, new BigInteger(1), "?", new BigInteger(3), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}