using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG03: cached member reads never go stale — mutable data arms resolve
/// current values on every access, and instance rebinding takes effect
/// immediately, in both modes.
/// </summary>
public sealed class MemberCacheBehaviorTests
{
    [Fact]
    public async Task ZipInfoFilenameStaysCurrent()
    {
        var script = new LythonEngine().Compile("""
            import zipfile
            z = zipfile.ZipInfo("a")
            outs = []
            outs.append(z.filename)
            z.filename = "b"
            outs.append(z.filename)
            return outs
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "a", "b" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task InstanceRebindingTakesEffect()
    {
        var script = new LythonEngine().Compile("""
            class A:
                def m(self):
                    return 1
            a = A()
            outs = []
            i = 0
            while i < 2:
                outs.append(a.m)
                a.m = 99
                i = i + 1
            return outs[1]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(99), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(99), asyncResult.ReturnValue);
    }
}
