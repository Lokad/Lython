using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: fixed class-level labels alias stably across accesses instead of
/// rebuilding fresh strings per read (random getstate tag, builtin exception
/// __module__), while the getstate tuple itself stays fresh per call.
/// </summary>
public sealed class SharedFixedLabelBehaviorTests
{
    [Fact]
    public async Task RandomStateTagIsShared()
    {
        var script = new LythonEngine().Compile("""
            import random
            s1 = random.getstate()
            s2 = random.getstate()
            random.setstate(s1)
            v = random.random()
            random.setstate(s2)
            w = random.random()
            return [s1[0] is s2[0], s1 is s2, s1[0], v == w]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, false, "lython.random.state", true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ExceptionModuleIsShared()
    {
        var script = new LythonEngine().Compile("""
            a = ValueError.__module__
            b = ValueError.__module__
            return [a is b, a]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, "builtins" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
