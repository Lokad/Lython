using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N13: exact accumulation and CPython result types for mean/fmean/median.
// Every expectation below was compared against local CPython 3.13 first;
// sync and async execution share the same paths and are both covered.
public sealed class StatisticsPrecisionScenarioTests
{
    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    [Fact]
    public async Task MeanCancelsLikeCpython()
    {
        var script = Compile(
            """
            import statistics
            return statistics.mean([1e16, 1.0, -1e16])
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(0.3333333333333333, Assert.IsType<double>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(0.3333333333333333, Assert.IsType<double>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task MeanKeepsHugeIntegersExact()
    {
        var script = Compile(
            """
            import statistics
            return [statistics.mean([2**100 + 1, 2**100 + 1]), statistics.mean([10**400, 10**400])]
            """);
        var expected = new List<object?>
        {
            BigInteger.Parse("1267650600228229401496703205377"),
            BigInteger.Parse("1" + new string('0', 400)),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MeanResultTypesFollowInputs()
    {
        var script = Compile(
            """
            import statistics
            return [
                statistics.mean([1.0, 3.0]),
                statistics.mean([1, 2, 3]),
                statistics.mean([1, 2.0]),
                statistics.mean([True, True]),
                statistics.mean([True, True, False]),
                statistics.mean([10**30, 1.5, -10**30]),
            ]
            """);
        var expected = new List<object?> { 2.0, new BigInteger(2), 1.5, new BigInteger(1), 0.6666666666666666, 0.5 };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MeanDecimalStaysDecimal()
    {
        var script = Compile(
            """
            import statistics
            from decimal import Decimal
            return [
                str(statistics.mean([Decimal(1), Decimal(2), Decimal(3)])),
                str(statistics.mean([Decimal(1), Decimal(2)])),
                str(statistics.mean([Decimal(1), 2])),
                str(statistics.fmean([Decimal("0.1"), Decimal("0.2")])),
            ]
            """);
        var expected = new List<object?> { "2", "1.5", "1.5", "0.15000000000000002" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MeanRejectsDecimalFloatMixing()
    {
        var script = Compile(
            """
            import statistics
            from decimal import Decimal
            results = []
            for data in ([Decimal(1), 1.0], [1.0, Decimal(1)]):
                try:
                    statistics.mean(data)
                    results.append("accepted")
                except TypeError:
                    results.append("TypeError")
            return results
            """);
        var expected = new List<object?> { "TypeError", "TypeError" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MeanOverflowAndSpecialContracts()
    {
        var script = Compile(
            """
            import math
            import statistics
            results = []
            for data in ([10**400, 1.5], [10**400, 10**400, 2]):
                try:
                    statistics.mean(data)
                    results.append("accepted")
                except OverflowError:
                    results.append("OverflowError")
            results.append(math.isinf(statistics.mean([float("inf"), 1.0])))
            results.append(math.isnan(statistics.mean([float("inf"), float("-inf")])))
            results.append(math.isnan(statistics.mean([float("nan")])))
            results.append(statistics.mean([1e308, 1e308]))
            results.append(statistics.fmean([1e16, 1.0, -1e16]))
            return results
            """);
        var expected = new List<object?> { "OverflowError", "OverflowError", true, true, true, 1e308, 0.3333333333333333 };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FMeanEdgeContracts()
    {
        var script = Compile(
            """
            import statistics
            results = []
            for data in ([1e308, 1e308], [1e308, 1e308, -1e308]):
                try:
                    statistics.fmean(data)
                    results.append("accepted")
                except OverflowError:
                    results.append("OverflowError")
            try:
                statistics.fmean([float("inf"), float("-inf")])
                results.append("accepted")
            except ValueError:
                results.append("ValueError")
            results.append(statistics.fmean([float("inf")]))
            results.append(statistics.fmean([-0.0, -0.0]) == 0.0)
            results.append(statistics.fmean([1, 2, 3], weights=[1, 1, 2]))
            return results
            """);
        var expected = new List<object?> { "OverflowError", "OverflowError", "ValueError", double.PositiveInfinity, true, 2.25 };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MedianPreservesOddElements()
    {
        var script = Compile(
            """
            import statistics
            from decimal import Decimal
            return [
                statistics.median([2**60 + 1, 2**60 + 3, 2**60 + 5]) == 2**60 + 3,
                statistics.median([True, False, True]),
                statistics.median([1.0, 2.0, 3.0]),
                str(statistics.median([Decimal(3), Decimal(1), Decimal(2)])),
                str(statistics.median([Decimal(3), 1, Decimal(2)])),
            ]
            """);
        var expected = new List<object?> { true, true, 2.0, "2", "2" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MedianEvenAveragesPerType()
    {
        var script = Compile(
            """
            import math
            import statistics
            from decimal import Decimal
            results = [
                statistics.median([1, 2, 3, 4]),
                str(statistics.median([Decimal(1), Decimal(2), Decimal(3), Decimal(4)])),
                statistics.median([True, False]),
                statistics.median([2**1023, 2**1023]),
                statistics.median([2**100 + 1, 2**100 + 2]),
            ]
            try:
                statistics.median([Decimal(1), 1.5])
                results.append("accepted")
            except TypeError:
                results.append("TypeError")
            try:
                statistics.median([10**400, 10**400])
                results.append("accepted")
            except OverflowError:
                results.append("OverflowError")
            results.append(math.isnan(statistics.median([3.0, float("nan"), 1.0])))
            return results
            """);
        var expected = new List<object?> { 2.5, "2.5", 0.5, 8.98846567431158e307, 1.2676506002282294e+30, "TypeError", "OverflowError", true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task IntegralVarianceKeepsIntType()
    {
        // N29: exact integral accumulation (CPython _ss): huge magnitudes stay
        // exact and integral results keep int type in both execution modes.
        var script = Compile(
            """
            import statistics
            return [statistics.pvariance([10**30, 10**30 + 2, 10**30 + 4]),
                    statistics.variance([10**30, 10**30 + 2, 10**30 + 4]),
                    statistics.pvariance([5]),
                    statistics.variance([1, 2, 3], 2),
                    statistics.variance([1, 2, 3], 2.0)]
            """);
        var expected = new List<object?>
        {
            2.6666666666666665,
            new BigInteger(4),
            new BigInteger(0),
            new BigInteger(1),
            1.0,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task EmptyVarianceReportsTwoPoints()
    {
        var script = Compile("import statistics\nstatistics.variance([])\n");
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("StatisticsError", sync.Failure?.ExceptionType);
        Assert.Contains("at least two data points", sync.Failure?.Message, StringComparison.Ordinal);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("StatisticsError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("at least two data points", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task QuantilesRenderFloatsAndKeepSinglePointType()
    {
        // N29: cut points are true-division floats (CPython); a single data
        // point returns the original element itself in both modes.
        var script = Compile(
            """
            import statistics
            return [statistics.quantiles([1, 2, 3, 4, 5]),
                    statistics.quantiles([1, 2, 3, 4, 5], method="inclusive"),
                    statistics.quantiles([1], n=4),
                    statistics.quantiles([2.5], n=4)]
            """);
        var expected = new List<object?>
        {
            new List<object?> { 1.5, 3.0, 4.5 },
            new List<object?> { 2.0, 3.0, 4.0 },
            new List<object?> { new BigInteger(1), new BigInteger(1), new BigInteger(1) },
            new List<object?> { 2.5, 2.5, 2.5 },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}