using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// Except-handler suites share the enclosing scope like CPython: assignments in
/// the handler stay visible afterwards in async runs exactly like sync runs and
/// probes, while only the `as` variable is deleted on suite exit.
/// </summary>
public sealed class ExceptHandlerScopeTests
{
    [Fact]
    public async Task HandlerRebindingSurvivesInBothModes()
    {
        var script = new LythonEngine().Compile(
            """
            flag = "no-stop"
            try:
                v = [1][5]
            except IndexError:
                flag = "stopped"
            return flag
            """);
        Assert.True(script.IsValid);

        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("stopped", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task HandlerRebindingSurvivesInsideFunctions()
    {
        var script = new LythonEngine().Compile(
            """
            def f():
                flag = "no-stop"
                try:
                    {}["k"]
                except KeyError:
                    flag = "stopped"
                return flag
            return f()
            """);
        Assert.True(script.IsValid);

        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("stopped", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AsVariableStillDeletedAfterSuite()
    {
        var script = new LythonEngine().Compile(
            """
            try:
                {}["k"]
            except KeyError as e:
                name = type(e).__name__
            try:
                e
                leaked = True
            except NameError:
                leaked = False
            return [name, leaked]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "KeyError", false };

        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        }
}
