using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: small container shells own their object while contents stay aliased
/// to existing owners; a view evaluated once per loop costs one unit.
/// </summary>
public sealed class ContainerShellAccountingScenarioTests
{
    // 30k retained key views own 64B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix.
    private const long ViewBudgetBytes = 1572864;

    // 20k retained defaultdicts own a 64B shell on top of the 192B dict base
    // and a 16B slot, so they fit 4.8MB pre-fix and trip post-fix.
    private const long DefaultDictBudgetBytes = 4800000;

    [Fact]
    public async Task ManyRetainedViewsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            d = {"a": 1, "b": 2}
            objs = []
            i = 0
            while i < 30000:
                objs.append(d.keys())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ViewBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ViewBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ViewBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedDefaultDictsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import collections
            objs = []
            i = 0
            while i < 20000:
                objs.append(collections.defaultdict(list))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = DefaultDictBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ContainerShellsBehave()
    {
        var script = new LythonEngine().Compile("""
            import collections
            d = {"a": 1, "b": 2}
            dd = collections.defaultdict(list)
            dd["k"].append(1)
            return [sorted(list(d.keys())), sorted(list(d.values())), len(list(d.items())), dd["k"], len(dd.copy())]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new List<object?> { "a", "b" }, new List<object?> { new BigInteger(1), new BigInteger(2) }, new BigInteger(2), new List<object?> { new BigInteger(1) }, new BigInteger(1) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}