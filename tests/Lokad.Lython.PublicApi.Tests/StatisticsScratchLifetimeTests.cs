using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N07 (statistics half): drained inputs (objects, doubles, weights, paired
// inputs) live in caller-scoped temporary reservations and release on every
// exit path, including validation failure. Before the fix, each discarded call
// stranded its drains and 1000 discards denied small budgets.
public sealed class StatisticsScratchLifetimeTests
{
    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static async Task AssertCompletes(string source, string expected, long maxBytes)
    {
        var script = Compile(source);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = maxBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task FMeanDiscards_SucceedUnderBudget()
        => await AssertCompletes(
            """
            import statistics
            for i in range(1000):
                statistics.fmean(range(1000))
            return "ok"
            """,
            "ok", 262144);

    [Fact]
    public async Task MedianDiscards_SucceedUnderBudget()
        => await AssertCompletes(
            """
            import statistics
            for i in range(1000):
                statistics.median(range(1000))
            return "ok"
            """,
            "ok", 262144);

    [Fact]
    public async Task WeightedFailRecover_SucceedsUnderBudget()
        => await AssertCompletes(
            """
            import statistics
            w = [1]*4999+["x"]
            d = list(range(5000))
            for i in range(20):
                try:
                    statistics.fmean(d, w)
                except (ValueError, TypeError):
                    pass
                statistics.fmean([1, 2])
            return "ok"
            """,
            "ok", 1048576);

    [Fact]
    public async Task PairedCovarianceDiscards_SucceedUnderBudget()
        => await AssertCompletes(
            """
            import statistics
            for i in range(20):
                statistics.covariance(range(5000), range(5000))
            return "ok"
            """,
            "ok", 1048576);

    [Fact]
    public async Task ScratchResultsStayCorrect()
    {
        // Drained-then-released inputs must compute exactly what funded,
        // never-drained runs compute, in both modes.
        var script = Compile("""
            import statistics
            d = [3, 1, 4, 1, 5, 9, 2, 6]
            return [statistics.fmean(d), statistics.median(d), statistics.median_low(d),
                statistics.median_high(d), statistics.mode(d), statistics.multimode(d),
                statistics.pvariance(d), statistics.stdev(d)]
            """);
        var expected = new List<object?>
        {
            3.875, 3.5, new BigInteger(3), new BigInteger(4), new BigInteger(1),
            new List<object?> { new BigInteger(1) }, 6.609375, 2.748376143938713,
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}