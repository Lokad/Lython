using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: directory entry names and paths build once and alias stably like
/// CPython (mirroring the cached stat), instead of rebuilding on every
/// access while charging every reread.
/// </summary>
public sealed class DirEntryIdentityBehaviorTests
{
    private static MockLythonHost SeededHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/d/f0", "x");
        return host;
    }

    [Fact]
    public async Task DirEntryMembersAliasStably()
    {
        var script = new LythonEngine().Compile("""
            import os
            found = None
            for e in os.scandir("/d"):
                found = e
            return [
                found.name is found.name, found.path is found.path,
                found.__fspath__() is found.path,
                found.name, found.path, found.is_file(),
            ]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, "f0", "/d/f0", true,
        };
        var sync = script.Run(SeededHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}