using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: operator factory products own their object and backing storage while
/// argument values stay aliased; dotted path parts derive from already-owned
/// argument payloads.
/// </summary>
public sealed class OperatorValueAccountingScenarioTests
{
    // 20k retained getters own ~200B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix.
    private const long GetterBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedItemGettersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import operator
            objs = []
            i = 0
            while i < 20000:
                objs.append(operator.itemgetter(1, "a"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = GetterBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= GetterBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= GetterBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedAttrGettersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import operator
            objs = []
            i = 0
            while i < 20000:
                objs.append(operator.attrgetter("a.b"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = GetterBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task OperatorFactoriesBehave()
    {
        var script = new LythonEngine().Compile("""
            import operator
            import collections
            P = collections.namedtuple("P", ["x", "y"])
            p = P(3, 4)
            getx = operator.attrgetter("x")
            geti = operator.itemgetter(1)
            up = operator.methodcaller("upper")
            return [getx(p), geti([10, 20]), up("hello"), geti((7, 8, 9))]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(3), new BigInteger(20), "HELLO", new BigInteger(8) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}