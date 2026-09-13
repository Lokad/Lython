using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// A def captures free variables transitively through intermediate def scopes,
/// including after every definer returned, and binds the nearest enclosing
/// cell on shadowing. Both modes.
/// </summary>
public sealed class TransitiveClosureCompatibilityTests
{
    [Fact]
    public async Task TransitiveDefCaptureSurvivesReturnedScopes()
    {
        var script = new LythonEngine().Compile("""
            def a():
                v = 1
                def b():
                    def c():
                        return v
                    return c
                return b()
            g = a()
            __lython_file = open("/out.txt", "w")
            __lython_file.write(str(g()))
            __lython_file.close()
            """);
        Assert.True(script.IsValid);

        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, Describe(sync));
        Assert.Equal("1", syncHost.ReadText("/out.txt"));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, Describe(asyncResult));
        Assert.Equal("1", asyncHost.ReadText("/out.txt"));
    }

    [Fact]
    public async Task DecoratorFixedParamsCaptureOuterScope()
    {
        var script = new LythonEngine().Compile("""
            def deco(tag):
                def wrap(fn):
                    def inner(x):
                        return (tag, fn(x))
                    return inner
                return wrap
            def raw(v):
                return v + 1
            f = deco("t")(raw)
            __lython_file = open("/out.txt", "w")
            __lython_file.write(str(f(21)))
            __lython_file.close()
            """);
        Assert.True(script.IsValid);

        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, Describe(sync));
        Assert.Equal("('t', 22)", syncHost.ReadText("/out.txt"));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, Describe(asyncResult));
        Assert.Equal("('t', 22)", asyncHost.ReadText("/out.txt"));
    }

    [Fact]
    public async Task NearestEnclosingRebindingWins()
    {
        var script = new LythonEngine().Compile("""
            def a():
                v = 1
                def b():
                    v = 2
                    def c():
                        return v
                    return c
                return b()
            h = a()
            __lython_file = open("/out.txt", "w")
            __lython_file.write(str(h()))
            __lython_file.close()
            """);
        Assert.True(script.IsValid);

        var syncHost = new MockLythonHost();
        var sync = script.Run(syncHost);
        Assert.True(sync.Success, Describe(sync));
        Assert.Equal("2", syncHost.ReadText("/out.txt"));

        var asyncHost = new MockLythonHost();
        var asyncResult = await script.RunAsync(asyncHost);
        Assert.True(asyncResult.Success, Describe(asyncResult));
        Assert.Equal("2", asyncHost.ReadText("/out.txt"));
    }

    private static string Describe(LythonExecutionResult result) =>
        result.Failure is null
            ? "run failed without details"
            : result.Failure.ExceptionType + ": " + result.Failure.Message;
}