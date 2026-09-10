using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: namedtuple-type fixed members (name, fields, field defaults) and
/// path-type names live on the constructed value instead of rebuilding on
/// every access, so repeated reads alias stably like CPython.
/// </summary>
public sealed class NamedTupleTypeIdentityBehaviorTests
{
    [Fact]
    public async Task NamedTupleTypeMembersAliasStably()
    {
        var script = new LythonEngine().Compile("""
            import collections
            from pathlib import Path
            N = collections.namedtuple("N", ["x", "y"], defaults=[2])
            M = collections.namedtuple("M", ["a"])
            return [
                N.__name__ is N.__name__, N._fields is N._fields,
                N._field_defaults is N._field_defaults,
                M._field_defaults == {},
                Path.__name__ is Path.__name__,
                N("a")._asdict() == {"x": "a", "y": 2},
                M("b").a,
            ]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            true, true, true, true, true, true, "b",
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}