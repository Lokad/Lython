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
    public async Task WideForwardedClosureRetainsSlotOrderAndMutableCells()
    {
        const string source = """
            def outer():
                a, b, c, d, e, f = 1, 2, 3, 4, 5, 6
                def middle():
                    def update():
                        nonlocal e
                        before = (f, e, d, c, b, a)
                        e += 100
                        return before, e
                    def clear():
                        nonlocal e
                        del e
                    return update, clear
                return middle()
            update, clear = outer()
            print(update())
            print(update())
            clear()
            try:
                update()
            except NameError:
                print('unbound')
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = budget };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, Describe(result));
                Assert.Equal("((6, 5, 4, 3, 2, 1), 105)\n((6, 105, 4, 3, 2, 1), 205)\nunbound\n", result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task LocalsSnapshotPreservesForwardedCaptureOrder()
    {
        using var script = new LythonEngine().Compile("""
            def outer():
                a = 1
                b = 2
                def middle():
                    read_b = lambda: b
                    def read_a(): return a
                    return tuple(locals()), read_a(), read_b()
                return middle()
            print(outer())
            """);
        Assert.True(script.IsValid);

        using var sync = script.Run(new MockLythonHost());
        using var asyncResult = await script.RunAsync(new MockLythonHost());
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.True(result.Success, Describe(result));
            Assert.Equal("(('read_b', 'read_a', 'a', 'b'), 1, 2)\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task LocalsSnapshotOrdersOrdinaryLocalsThenCellsThenFreeNames()
    {
        using var script = new LythonEngine().Compile("""
            def outer(parameter):
                zebra = 1
                alpha = 2
                def middle():
                    def first(): return zebra
                    def second(): return alpha
                    print(tuple(locals()), first(), second(), parameter)
                plain = 3
                print(tuple(locals()))
                middle()
            outer(7)
            """);
        Assert.True(script.IsValid);
        const string expected = "('parameter', 'middle', 'plain', 'alpha', 'zebra')\n" +
                                "('first', 'second', 'alpha', 'parameter', 'zebra') 1 2 7\n";
        using var sync = script.Run(new MockLythonHost());
        using var asyncResult = await script.RunAsync(new MockLythonHost());
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.True(result.Success, Describe(result));
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Fact]
    public async Task TransitiveDefCaptureSurvivesReturnedScopes()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
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
