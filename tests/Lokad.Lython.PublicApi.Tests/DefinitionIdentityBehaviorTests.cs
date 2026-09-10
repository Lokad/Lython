using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: definition-fixed function/type members (names, bases, mro) live on
/// the constructed value instead of rebuilding on every access, so repeated
/// reads alias stably like CPython.
/// </summary>
public sealed class DefinitionIdentityBehaviorTests
{
    [Fact]
    public async Task DefinitionMembersAliasStably()
    {
        var script = new LythonEngine().Compile("""
            def f():
                pass
            class C:
                x = 1
            class D(C):
                pass
            f.__name__ = "g"
            return [
                f.__name__ is f.__name__, f.__qualname__ is f.__qualname__,
                C.__name__ is C.__name__, C.__bases__ is C.__bases__,
                C.__mro__ is C.__mro__, D.__bases__[0] is C,
                f.__name__, C.__name__, len(C.__mro__),
            ]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true,
            "g", "C", new BigInteger(2),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}