using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N15: statistics consumers compose through async suspension instead of failing
// with synchronous-read guidance. Each awaited path shares its drain,
// validation and computation facts with the sync twin; only iteration suspends.
public sealed class StatisticsAsyncCompositionTests
{
    private static DelayedLythonHost SeedDelayedHost()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/r.txt", "3\n1\n4\n1\n5\n");
        host.SeedFile("/n.txt", "3\n1\n4\n");
        host.SeedFile("/m.txt", "1\n2\n3\n");
        return host;
    }

    private static MockLythonHost SeedSyncHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/r.txt", "3\n1\n4\n1\n5\n");
        host.SeedFile("/n.txt", "3\n1\n4\n");
        host.SeedFile("/m.txt", "1\n2\n3\n");
        return host;
    }

    private static async Task AssertAsyncMatchesSync(string source, object? expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(SeedSyncHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = SeedDelayedHost();
        var asyncResult = await script.RunAsync(delayed);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task MeanOverSuspendingGeneratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/r.txt\") as f:\n    return statistics.mean(int(s) for s in f)\n",
            2.8);
    }

    [Fact]
    public async Task FMeanOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/r.txt\") as f:\n    return statistics.fmean(int(s) for s in f)\n",
            2.8);
    }

    [Fact]
    public async Task MedianOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/r.txt\") as f:\n    return [statistics.median(int(s) for s in f), statistics.median_low(int(s) for s in open(\"/r.txt\")), statistics.median_high(int(s) for s in open(\"/r.txt\"))]\n",
            new List<object?> { new BigInteger(3), new BigInteger(3), new BigInteger(3) });
    }

    [Fact]
    public async Task ModeOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/r.txt\") as f:\n    return [statistics.mode(int(s) for s in f), statistics.multimode(int(s) for s in open(\"/r.txt\"))]\n",
            new List<object?> { new BigInteger(1), new List<object?> { new BigInteger(1) } });
    }

    [Fact]
    public async Task VarianceOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/m.txt\") as f:\n    return [statistics.variance(int(s) for s in f), statistics.pvariance(int(s) for s in open(\"/m.txt\"))]\n",
            // Integral inputs accumulate exactly (CPython _ss), so integral results
            // are ints; the test pins composition and exact values in both modes.

            new List<object?> { new BigInteger(1), 0.6666666666666666 });
    }

    [Fact]
    public async Task HarmonicAndGeometricOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/n.txt\") as f:\n    return [statistics.harmonic_mean(int(s) for s in f), statistics.geometric_mean(int(s) for s in open(\"/n.txt\"))]\n",
            new List<object?> { 1.8947368421052633, 2.2894284851066637 });
    }

    // Quantile cut points keep the pinned whole-value int rendering (quantiles is
    // intentionally untouched by N13), so the cut arrives as an integer here.
    [Fact]
    public async Task QuantilesOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/r.txt\") as f:\n    return statistics.quantiles((int(s) for s in f), n=2)\n",
            new List<object?> { new BigInteger(3) });
    }

    [Fact]
    public async Task CovarianceOverSuspendingIteratorsComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/m.txt\") as f:\n    return statistics.covariance((int(s) for s in f), (int(s) for s in open(\"/m.txt\")))\n",
            1.0);
    }

    [Fact]
    public async Task FromSamplesOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import statistics\nwith open(\"/n.txt\") as f:\n    return statistics.NormalDist.from_samples(int(s) for s in f).mean\n",
            2.6666666666666665);
    }

    [Theory]
    [InlineData("import statistics\nwith open(\"/r.txt\") as f:\n    return statistics.mean(int(s) for s in f)\n")]
    [InlineData("import statistics\nwith open(\"/r.txt\") as f:\n    return statistics.fmean(int(s) for s in f)\n")]
    [InlineData("import statistics\nwith open(\"/r.txt\") as f:\n    return statistics.median(int(s) for s in f)\n")]
    [InlineData("import functools\nwith open(\"/r.txt\") as f:\n    return functools.reduce(lambda a, b: a + int(b), f, 0)\n")]
    public void SyncRunsFailFastWithRunAsyncGuidance(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var result = script.Run(SeedDelayedHost());
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("use RunAsync", result.Failure?.Message, StringComparison.Ordinal);
    }
}
