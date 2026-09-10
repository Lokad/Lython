using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: functools factory products own their object shell beside any governed
/// backing while callables and argument values stay aliased.
/// </summary>
public sealed class PartialValueAccountingScenarioTests
{
    // 20k retained partials own a 64B shell plus 64B of backing and a 16B list
    // slot each, so they fit 2MiB pre-fix and trip post-fix.
    private const long PartialBudgetBytes = 2097152;

    [Fact]
    public async Task ManyRetainedPartialsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import functools
            def add(a, b):
                return a + b
            objs = []
            i = 0
            while i < 20000:
                objs.append(functools.partial(add, i))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = PartialBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= PartialBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= PartialBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedPartialMethodsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import functools
            def add(a, b):
                return a + b
            objs = []
            i = 0
            while i < 20000:
                objs.append(functools.partialmethod(add, i))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = PartialBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task PartialFactoriesBehave()
    {
        var script = new LythonEngine().Compile("""
            import functools
            def add(a, b):
                return a + b
            p = functools.partial(add, 10)
            pm = functools.partialmethod(add, 10)
            return [p(1), p(2), str(pm)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new System.Numerics.BigInteger(11), new System.Numerics.BigInteger(12), "functools.partialmethod(...)" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}