using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: per-call *args tuples and **kwargs dicts. Dropped arguments must
/// fit a fixed budget across calls; retained arguments must stay charged.
/// </summary>
public sealed class KwargsAccountingScenarioTests : LifetimeScenario
{
    [Fact]
    public async Task PlainCallControlFits()
    {
        var script = new LythonEngine().Compile(
            """
            def f(a, b):
                return a
            i = 0
            n = 0
            while i < 20000:
                n = n + f(1, 2)
                i = i + 1
            return n
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
    [Fact]
    public async Task PlainLoopControlFits()
    {
        var script = new LythonEngine().Compile(
            """
            i = 0
            n = 0
            while i < 20000:
                n = n + 1
                i = i + 1
            return n
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
    [Fact]
    public async Task DroppedStarargsDoNotAccumulate()
    {
        await AssertBatchCompletes("""
            def f(*a):
                return len(a)
            i = 0
            n = 0
            while i < 8:
                n = n + f(1, 2)
                i = i + 1
            return n
            """, new System.Numerics.BigInteger(16), 1048576);
    }
    [Fact]
    public async Task DroppedKwargsDoNotAccumulate()
    {
        await AssertBatchCompletes("""
            def f(**k):
                return len(k)
            i = 0
            n = 0
            while i < 8:
                n = n + f(a=1, b=2)
                i = i + 1
            return n
            """, new System.Numerics.BigInteger(16), 1048576);
    }

    [Fact]
    public async Task RetainedStarargsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            out = []
            def f(*a):
                out.append(a)
            i = 0
            while i < 20000:
                f(1, 2)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RetainedKwargsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            out = []
            def f(**k):
                out.append(k)
            i = 0
            while i < 20000:
                f(a=1, b=2)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task KwargsBehaviorsProject()
    {
        var script = new LythonEngine().Compile(
            """
            def f(*a, **k):
                return (len(a), len(k), k["x"])
            return f(1, 2, x=3)
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }
}
