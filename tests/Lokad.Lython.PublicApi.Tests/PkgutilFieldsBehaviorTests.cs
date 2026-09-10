using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: ModuleInfo fixed labels are class-level shared constants (like
/// namedtuple _fields), so per-access reads cost nothing and alias stably
/// instead of allocating fresh tuples and strings on every access.
/// </summary>
public sealed class PkgutilFieldsBehaviorTests
{
    [Fact]
    public async Task FieldsTupleIsShared()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            a = pkgutil.ModuleInfo(None, "m", False)
            b = pkgutil.ModuleInfo(None, "n", True)
            return [a._fields is b._fields, a._fields is a._fields, list(a._fields)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, new List<object?> { "module_finder", "name", "ispkg" } };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AsDictBehaves()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            mi = pkgutil.ModuleInfo(None, "m", True)
            d = mi._asdict()
            return [d["module_finder"], d["name"], d["ispkg"], mi._replace(name="sub").name]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { null, "m", true, "sub" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
