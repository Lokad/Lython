using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: module fixed vocabulary and identity-stable singletons live as
/// shared constants (or per-run cached values), so per-access reads alias
/// stably instead of allocating fresh objects on every access, and sys.path
/// mutations persist like CPython.
/// </summary>
public sealed class ModuleSingletonBehaviorTests
{
    [Fact]
    public async Task ModuleSingletonsAliasStably()
    {
        var script = new LythonEngine().Compile("""
            import os
            import sys
            sys.path.append("/x")
            return [
                os.pardir is os.pardir, os.sep is os.sep, os.name is os.name,
                sys.version is sys.version, sys.platform is sys.platform,
                sys.implementation is sys.implementation,
                sys.version_info is sys.version_info,
                sys.path is sys.path, "/x" in sys.path,
                os.pardir, sys.platform, sys.implementation.name,
                list(sys.version_info)[:3],
            ]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, true, true, true,
            "..", "lython", "lython",
            new List<object?> { new BigInteger(3), new BigInteger(13), new BigInteger(0) },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}