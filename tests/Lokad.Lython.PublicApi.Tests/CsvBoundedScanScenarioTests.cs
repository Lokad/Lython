using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG01/MG21: full scans retain only the current row, so the CSV and text
// pools participate in exhaustion relief and bounded scans complete under a
// small fixed budget. Retained output must still trip; collection counts and
// allocations below ride failure messages only (process-global counters are
// meaningless for pass-path asserts under parallel tests).
public class CsvBoundedScanScenarioTests
{
    protected virtual long MemoryLimit => 65536;
    protected virtual int SmallRows => 512;
    protected virtual int LargeRows => 2048;

    private static string ScanSource(int rows)
        => "import csv\nr = csv.reader('a,b,c,d,e,f\\n' for i in range(" + rows + "))\ntotal = 0\nfor row in r:\n    total += 1\nreturn total\n";

    private static string Describe(LythonExecutionResult result, long gen2, long allocated)
        => "success=" + result.Success + " type=" + result.Failure?.ExceptionType + " msg=" + result.Failure?.Message
        + " peak=" + result.PeakExecutionMemoryBytes + " gen2=" + gen2 + " alloc=" + allocated;

    private void AssertMemoryError(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.True(result.PeakExecutionMemoryBytes <= MemoryLimit);
    }

    [Fact]
    public async Task FullScanCompletesAtSmallScale()
    {
        var script = new LythonEngine().Compile(ScanSource(SmallRows));
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(SmallRows);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MemoryLimit };
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
    public async Task FullScanCompletesAtLargeScale()
    {
        var script = new LythonEngine().Compile(ScanSource(LargeRows));
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(LargeRows);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MemoryLimit };
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
            "import csv\nr = csv.reader('a,b,c,d,e,f\\n' for i in range(" + SmallRows + "))\nrows = list(r)\nreturn len(rows)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MemoryLimit };
        AssertMemoryError(script.Run(new MockLythonHost(), options));
        AssertMemoryError(await script.RunAsync(new MockLythonHost(), options));
    }

    [Fact]
    public async Task DelayedFileScanCompletes()
    {
        var content = new StringBuilder();
        for (var i = 0; i < SmallRows; i++)
        {
            content.Append("a,b,c,d,e,f\n");
        }

        var script = new LythonEngine().Compile(
            "import csv\nwith open(\"/r.csv\") as f:\n    total = 0\n    for row in csv.reader(f):\n        total += 1\nreturn total\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(SmallRows);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MemoryLimit };
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
            "import csv\nok = False\ntry:\n    rows = list(csv.reader('a,b,c,d,e,f\\n' for i in range(" + SmallRows + ")))\nexcept MemoryError:\n    ok = True\nreturn ok\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MemoryLimit };
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
        // Exact completion at a fixed cap is the leak oracle. Peak ratios are
        // a secondary historical stress check and do not prove host latency.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MemoryLimit };
        var smallPeak = await ScanPeak(ScanSource(SmallRows), SmallRows, options);
        var largePeak = await ScanPeak(ScanSource(LargeRows), LargeRows, options);
        Assert.True(largePeak <= 4 * smallPeak, "small peak=" + smallPeak + " large peak=" + largePeak);
        Assert.True(largePeak <= MemoryLimit, "large peak=" + largePeak);
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