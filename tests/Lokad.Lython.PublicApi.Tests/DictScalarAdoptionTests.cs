using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N06 (PyDict half): governed dicts adopt one uniform coupon per distinct
// small-scalar identity held as a key or a value. Distinct populations deny
// before their retention escapes the budget; shared boxes adopt once across
// both halves; replacement turns over the value coupon while the original key
// stays put; removals, pops and clears release.
public sealed class DictScalarAdoptionTests
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
        var script = Compile("return len({i: i for i in range(100000)})\n");
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
    public async Task SharedKeyValueAdoptsOnce()
    {
        // The same box flows as key and value (refcount 2, one coupon), and
        // repeated replacement of one entry never accumulates.
        var script = Compile("""
            d = {}
            k = 7
            v = 8
            for i in range(100000):
                d[k] = v
            return [len(d), d[k] is v]
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var expected = new List<object?> { new BigInteger(1), true };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ReplaceTurnsOverValueCoupons()
    {
        var script = Compile("""
            d = {i: i for i in range(1000)}
            for i in range(1000):
                d[i] = -i
            return [len(d), d[5], d[5] == -5]
            """);
        var expected = new List<object?> { new BigInteger(1000), new BigInteger(-5), true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ClearRefillReleasesAdoptedCoupons()
    {
        var script = Compile("""
            d = {}
            for c in range(10):
                d.clear()
                for i in range(5000):
                    d[i + c * 5000] = i
            return len(d)
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2097152 };
        var expected = new BigInteger(5000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task PopAndUpdateBehave()
    {
        var script = Compile("""
            d = {1: 10, 2: 20, 3: 30}
            out = []
            out.append(d.pop(2))
            d.update({4: 40})
            d.setdefault(5, 50)
            d.setdefault(1, 99)
            k, v = d.popitem()
            out.append([len(d), d[1], d[4], d[3], k, v])
            return out
            """);
        var expected = new List<object?>
        {
            new BigInteger(20),
            new List<object?> { new BigInteger(3), new BigInteger(10), new BigInteger(40), new BigInteger(30), new BigInteger(5), new BigInteger(50) },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CopySharesBoxesConservatively()
    {
        var script = Compile("""
            d = {1: 10, 2: 20}
            e = dict(d)
            return [e == d, e[1] is d[1], len(e)]
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