using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG01/MG21: full scans retain only the current row, so the CSV and text
// pools participate in exhaustion relief and bounded scans complete under a
// small fixed budget. Retained output must still trip; collection counts and
// allocations below ride failure messages only (process-global counters are
// meaningless for pass-path asserts under parallel tests).
public sealed class CsvBoundedScanScenarioTests
{
    private const long ThreeMib = 3145728;

    private static string ScanSource(int rows)
        => "import csv\nr = csv.reader('a,b,c,d,e,f\\n' for i in range(" + rows + "))\ntotal = 0\nfor row in r:\n    total += 1\nreturn total\n";

    private static string Describe(LythonExecutionResult result, long gen2, long allocated)
        => "success=" + result.Success + " type=" + result.Failure?.ExceptionType + " msg=" + result.Failure?.Message
        + " peak=" + result.PeakExecutionMemoryBytes + " gen2=" + gen2 + " alloc=" + allocated;

    private static void AssertMemoryError(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.True(result.PeakExecutionMemoryBytes <= ThreeMib);
    }

    [Fact]
    public async Task FullScanCompletesAt20k()
    {
        var script = new LythonEngine().Compile(ScanSource(20000));
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(20000);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var gen0 = GC.CollectionCount(2);
        var alloc0 = GC.GetAllocatedBytesForCurrentThread();
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, Describe(sync, GC.CollectionCount(2) - gen0, GC.GetAllocatedBytesForCurrentThread() - alloc0));
        Assert.Equal(expected, sync.ReturnValue);

        gen0 = GC.CollectionCount(2);
        alloc0 = GC.GetAllocatedBytesForCurrentThread();
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, Describe(asyncResult, GC.CollectionCount(2) - gen0, GC.GetAllocatedBytesForCurrentThread() - alloc0));
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FullScanCompletesAt200k()
    {
        var script = new LythonEngine().Compile(ScanSource(200000));
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(200000);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var gen0 = GC.CollectionCount(2);
        var alloc0 = GC.GetAllocatedBytesForCurrentThread();
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, Describe(sync, GC.CollectionCount(2) - gen0, GC.GetAllocatedBytesForCurrentThread() - alloc0));
        Assert.Equal(expected, sync.ReturnValue);

        gen0 = GC.CollectionCount(2);
        alloc0 = GC.GetAllocatedBytesForCurrentThread();
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, Describe(asyncResult, GC.CollectionCount(2) - gen0, GC.GetAllocatedBytesForCurrentThread() - alloc0));
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedScanRowsStayCharged()
    {
        var script = new LythonEngine().Compile(
            "import csv\nr = csv.reader('a,b,c,d,e,f\\n' for i in range(20000))\nrows = list(r)\nreturn len(rows)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        AssertMemoryError(script.Run(new MockLythonHost(), options));
        AssertMemoryError(await script.RunAsync(new MockLythonHost(), options));
    }

    [Fact]
    public async Task DelayedFileScanCompletes()
    {
        var content = new StringBuilder();
        for (var i = 0; i < 20000; i++)
        {
            content.Append("a,b,c,d,e,f\n");
        }

        var script = new LythonEngine().Compile(
            "import csv\nwith open(\"/r.csv\") as f:\n    total = 0\n    for row in csv.reader(f):\n        total += 1\nreturn total\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(20000);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var host = new DelayedLythonHost();
        host.SeedFile("/r.csv", content.ToString());
        var asyncResult = await script.RunAsync(host, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);

        var host2 = new MockLythonHost();
        host2.SeedFile("/r.csv", content.ToString());
        var sync = script.Run(host2, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
    }

    // The tail returns a pre-existing singleton: headroom after a caught
    // denial depends on collection timing, so reuse tails must not assume
    // fresh allocations fit.
    [Fact]
    public async Task CaughtScanFailureAllowsReuse()
    {
        var script = new LythonEngine().Compile(
            "import csv\nok = False\ntry:\n    rows = list(csv.reader('a,b,c,d,e,f\\n' for i in range(20000)))\nexcept MemoryError:\n    ok = True\nreturn ok\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(true, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(true, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundedScanPeakStaysFlatAcrossSizes()
    {
        // Bounded scans retain only the current row: the 10x scan must not peak
        // an order of magnitude above the 1x scan (a linear strand would show
        // ~10x). Peaks are accounted math, but relief timing can wobble them,
        // so the slope bound stays loose. The 200k peak rides near the budget
        // ceiling (relief triggers at exhaustion) while the 20k peak is a
        // GC-timed transient, so Windows CI shows ~2.6x. Bounded old-tier sweeps
        // add release lag on top (measured 3.16x Debug, green Release on the same
        // tree); 4x still catches linear retention (which would deny long before
        // completing: a 400k scan at double budget completes with its peak
        // pinned at the ceiling by relief, not by rows).
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var peak20k = await ScanPeak(ScanSource(20000), 20000, options);
        var peak200k = await ScanPeak(ScanSource(200000), 200000, options);
        Assert.True(peak200k <= 4 * peak20k, "20k peak=" + peak20k + " 200k peak=" + peak200k);
        Assert.True(peak200k <= ThreeMib, "200k peak=" + peak200k);
    }

    private static async Task<long> ScanPeak(string source, int rows, LythonRunOptions options)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(rows);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        return Math.Max(sync.PeakExecutionMemoryBytes, asyncResult.PeakExecutionMemoryBytes);
    }
}