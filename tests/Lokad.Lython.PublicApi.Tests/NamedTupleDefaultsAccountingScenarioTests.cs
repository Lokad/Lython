using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG05: namedtuple drains defaults at most one past the field count, so an
/// oversized lazy defaults iterable raises the same TypeError without a
/// proportional transient. Field names parse first, matching CPython
/// left-to-right validation.
/// </summary>
public sealed class NamedTupleDefaultsAccountingScenarioTests
{
    [Fact]
    public async Task DefaultsBehaveLikeBefore()
    {
        var script = new LythonEngine().Compile(
            """
            import collections
            P = collections.namedtuple("P", ["a", "b"], defaults=[10])
            p = P(1)
            Q = collections.namedtuple("Q", ["x", "y"])
            q = Q(1, 2)
            return [p.a, p.b, q.x, q.y]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(10), new BigInteger(1), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task TooManyDefaultsKeepTheirError()
    {
        var script = new LythonEngine().Compile(
            """
            import collections
            P = collections.namedtuple("P", ["a", "b"], defaults=[1, 2, 3])
            return 0
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("TypeError", sync.Failure?.ExceptionType);
        Assert.Equal("collections.namedtuple(..., defaults=...) has more defaults than fields.", sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("TypeError", asyncResult.Failure?.ExceptionType);
        Assert.Equal("collections.namedtuple(..., defaults=...) has more defaults than fields.", asyncResult.Failure?.Message);
    }
}
