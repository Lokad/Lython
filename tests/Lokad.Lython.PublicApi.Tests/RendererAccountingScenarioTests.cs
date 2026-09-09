using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG06: renderers that dropped their rendering context built ungoverned
/// strings. Routing them through the owning governor must not change a byte
/// of output, and legitimately large reprs must still succeed when funded.
/// </summary>
public sealed class RendererAccountingScenarioTests
{
    [Fact]
    public async Task RendererContractsStayExact()
    {
        // MG06: governed joins, dataclass reprs and the singleton-tuple shape
        // render byte-identically to before.
        var script = new LythonEngine().Compile(
            """
            import operator
            import collections
            from dataclasses import dataclass

            @dataclass
            class Box:
                name: str
                index: int

            g = operator.itemgetter(0, 1)
            t = (7,)
            c = collections.Counter({"a": 2})
            d = collections.defaultdict(int, {1: 2})
            m = collections.ChainMap({"a": 1})
            box = Box("x", 5)
            return [repr(g), repr(t), repr(c), repr(d), repr(m), repr(box)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?>
        {
            "operator.itemgetter(0, 1)",
            "(7,)",
            "Counter({'a': 2})",
            "defaultdict(int, {1: 2})",
            "ChainMap({'a': 1})",
            "Box(name='x', index=5)",
        };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RendererScaleSucceedsWhenFunded()
    {
        // MG06: governed joins must not break legitimately large reprs; a
        // 2,000-entry defaultdict repr stays proportional and succeeds funded.
        var script = new LythonEngine().Compile(
            """
            import collections
            d = collections.defaultdict(int)
            for i in range(2000):
                d[i] = i
            r = repr(d)
            m = collections.ChainMap({"a": 1})
            s = repr(m)
            return [len(r) > 10000, len(s) > 10]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16777216 };
        var expected = new List<object?> { true, true };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task JoinContractsStayExact()
    {
        // MG06: routing str.join through the owning governor must not change
        // joined output or the non-string rejection.
        var script = new LythonEngine().Compile(
            """
            results = []
            results.append(",".join(["a", "b", "c"]))
            results.append("-".join([]))
            results.append(":".join(["x"]))
            try:
                ",".join(["a", 1])
            except TypeError:
                results.append("nonstring")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { "a,b,c", "", "x", "nonstring" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task JoinScaleSucceedsWhenFunded()
    {
        // MG06: governed joins must not break legitimately large joins; a
        // 2,000-part join stays proportional and succeeds funded.
        var script = new LythonEngine().Compile(
            """
            parts = ["abcdefghij"] * 2000
            r = ",".join(parts)
            return len(r)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(21999), Assert.IsType<BigInteger>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(21999), Assert.IsType<BigInteger>(asyncResult.ReturnValue));
    }
}
