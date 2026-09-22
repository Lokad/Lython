using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N06 (PyList half): governed lists adopt one uniform coupon per distinct
// small-scalar identity they retain. A population of distinct boxes denies
// before its retention exceeds the budget; aliases of one box share a single
// coupon; released slots drop their coupons so refill loops stay bounded.
public sealed class ListScalarAdoptionTests
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
        // 100k distinct small ints retain ~4.4 MB; coupons (~6.4 MB) deny at
        // 3 MiB before the retention escapes the budget.
        var script = Compile("return len([i for i in range(100000)])\n");
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
        // 100k aliases of one box hold a single coupon and stay green.
        var script = Compile("return len([0] * 100000)\n");
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var expected = new BigInteger(100000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SliceRefillReleasesAdoptedCoupons()
    {
        // Each refill drops 10k distinct identities; released coupons keep
        // ten refills bounded under 1.5 MiB (peak ~0.9 MiB).
        var script = Compile("""
            lst = []
            for c in range(10):
                del lst[:]
                for i in range(10000):
                    lst.append(i + c * 10000)
            return len(lst)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1572864 };
        var expected = new BigInteger(10000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AliasIdentitySurvivesAdoption()
    {
        // Adoption is charge-only: aliases keep identity, removal keeps the
        // surviving reference observable.
        var script = Compile("""
            x = 12345
            lst = [x, x, 1]
            first = lst[0] is lst[1]
            del lst[0]
            return [first, lst[0] is x, len(lst)]
            """);
        var expected = new List<object?> { true, true, new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}