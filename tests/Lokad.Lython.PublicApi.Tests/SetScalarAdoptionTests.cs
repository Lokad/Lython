using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N06 (PySet half): governed sets adopt one uniform coupon per distinct
// small-scalar identity they retain. A population of distinct boxes denies
// before its retention escapes the budget; aliases share a single coupon;
// removed, discarded and cleared identities release, so refill loops stay
// bounded; algebra results adopt their own contents.
public sealed class SetScalarAdoptionTests
{
    private const long ThreeMib = 3145728;

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public async Task DistinctPopulationDeniedBeforeRetention()
    {
        var script = Compile("return len({i for i in range(100000)})\n");
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
    public async Task AliasPopulationSharesOneCoupon()
    {
        var script = Compile("""
            s = set()
            for i in range(100000):
                s.add(0)
            return len(s)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var expected = new BigInteger(1);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ClearRefillReleasesAdoptedCoupons()
    {
        var script = Compile("""
            s = set()
            for c in range(10):
                s.clear()
                for i in range(10000):
                    s.add(i + c * 10000)
            return len(s)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2097152 };
        var expected = new BigInteger(10000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DiscardAllReleasesAdoptedCoupons()
    {
        var script = Compile("""
            s = {i for i in range(10000)}
            for i in range(10000):
                s.discard(i)
            return len(s)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SetAlgebraAdoptsResults()
    {
        var script = Compile("""
            a = {1, 2, 3}
            b = {3, 4, 5}
            return [sorted(a | b), sorted(a & b), sorted(a - b), 3 in a]
            """);
        var expected = new List<object?>
        {
            new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(3), new BigInteger(4), new BigInteger(5) },
            new List<object?> { new BigInteger(3) },
            new List<object?> { new BigInteger(1), new BigInteger(2) },
            true,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}