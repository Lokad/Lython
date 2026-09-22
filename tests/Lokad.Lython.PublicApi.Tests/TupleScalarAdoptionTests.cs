using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N06 (PyTuple half): governed tuples adopt one uniform coupon per distinct
// small-scalar identity at construction. Tuples never mutate, so the total
// stays fixed and no refcount map is retained: only the total rides the drop
// snapshot. Distinct populations deny; alias repetitions share one coupon;
// unpacking loops stay bounded.
public sealed class TupleScalarAdoptionTests
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
        var script = Compile("return len(tuple(range(100000)))\n");
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
    public async Task AliasRepeatSharesOneCoupon()
    {
        var script = Compile("return len((0,) * 100000)\n");
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
    public async Task UnpackingLoopStaysBounded()
    {
        var script = Compile("""
            total = 0
            for k, v in {i: i for i in range(1000)}.items():
                total = total + 1
            return total
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2097152 };
        var expected = new BigInteger(1000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task TupleSemanticsPreserved()
    {
        var script = Compile("""
            t = (1, 2, 3)
            a, b, c = t
            u = (4,) + t
            return [len(t), t[0], t[-1], a + b + c, t.count(2), t.index(3), len(u), u[0]]
            """);
        var expected = new List<object?>
        {
            new BigInteger(3),
            new BigInteger(1),
            new BigInteger(3),
            new BigInteger(6),
            new BigInteger(1),
            new BigInteger(2),
            new BigInteger(4),
            new BigInteger(4),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}