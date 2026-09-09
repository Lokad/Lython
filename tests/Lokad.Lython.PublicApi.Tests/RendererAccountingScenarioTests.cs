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
    [Fact]
    public async Task RetainedIntReprsStayCharged()
    {
        // MG06 probe: 30 retained reprs of a 20KB integer hold ~600KB of
        // digit text beside a small charged source under a 64KiB budget.
        var script = new LythonEngine().Compile(
            """
            big = 10 ** 20000
            out = []
            for i in range(30):
                out.append(repr(big))
            return len(out)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ScalarRendererContractsStayExact()
    {
        // MG06: governing scalar outputs must not change rendered values,
        // including interpolated ints, doubles, and exception messages.
        var script = new LythonEngine().Compile(
            """
            results = []
            results.append(repr(12345678901234567890123))
            results.append(repr(1.5))
            results.append(str(2.5))
            results.append(f"{10 ** 30}")
            try:
                raise ValueError("y" * 5000)
            except ValueError as e:
                results.append(len(str(e)))
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?>
        {
            "12345678901234567890123",
            "1.5",
            "2.5",
            "1000000000000000000000000000000",
            new BigInteger(5000),
        };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task RetainedDecimalReprsStayCharged()
    {
        // MG06 probe: 1,000 retained decimal reprs hold ~40KB of text beside
        // small charged sources under a 64KiB budget.
        var script = new LythonEngine().Compile(
            """
            from decimal import Decimal
            d = Decimal("1.234567890123456789012345678")
            out = []
            for i in range(1000):
                out.append(repr(d))
            return len(out)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DecimalRendererContractsStayExact()
    {
        // MG06: governing decimal rendering must not change repr/str output.
        var script = new LythonEngine().Compile(
            """
            from decimal import Decimal
            d = Decimal("1.5")
            e = Decimal("0.000001")
            return [repr(d), str(d), repr(e), str(e)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { "Decimal('1.5')", "1.5", "Decimal('0.000001')", "0.000001" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}
