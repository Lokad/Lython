using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N06 (namedtuple half): guest-constructed records adopt one uniform coupon per
// distinct small-scalar field. Records never mutate, so the total stays fixed;
// drops release through the regular tracking snapshot.
public sealed class NamedTupleScalarAdoptionTests
{
    private const long ThreeMib = 3145728;

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public async Task DistinctRowsDeniedBeforeRetention()
    {
        var script = Compile("""
            import collections
            P = collections.namedtuple("P", ["a", "b"])
            rows = [P(i, i + 1) for i in range(50000)]
            return len(rows)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ThreeMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ThreeMib);
    }

    [Fact]
    public async Task AliasFieldsShareOneCoupon()
    {
        var script = Compile("""
            import collections
            P = collections.namedtuple("P", ["a", "b"])
            x = 5
            rows = [P(x, x) for _ in range(1000)]
            return [len(rows), rows[0][0] is x]
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { new BigInteger(1000), true };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}