using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Long scans use host-owned finite executions. No guest-reference withdrawal
// or collector timing is required to make the next batch admissible.
public class CsvBoundedScanScenarioTests
{
    protected virtual long MemoryLimit => 65536;
    protected virtual int SmallRows => 512;
    protected virtual int LargeRows => 2048;
    protected virtual int BatchRows => 16;

    private static string ScanSource(int rows)
        => "import csv\nr = csv.reader('a,b,c,d,e,f\\n' for i in range(" + rows + "))\ntotal = 0\nfor row in r:\n    total += 1\nreturn total\n";

    [Fact]
    public async Task HostBatchesCompleteAtSmallScale()
        => await ScanBatches(SmallRows);

    [Fact]
    public async Task HostBatchesCompleteAtLargeScale()
        => await ScanBatches(LargeRows);

    [Fact]
    public async Task RetainedScanRowsStayCharged()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import csv\nr = csv.reader('a,b,c,d,e,f\\n' for i in range(" + SmallRows + "))\nrows = list(r)\nreturn len(rows)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(MemoryLimit) };
        using var sync = script.Run(new MockLythonHost(), options);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.InRange(result.PeakExecutionMemoryBytes, 1, MemoryLimit);
        }
    }

    [Fact]
    public async Task DelayedFileBatchesComplete()
        => await ScanBatches(SmallRows, file: true);

    [Fact]
    public async Task RefusedScanFailsButLaterCommandsReuseTheBudget()
    {
        var budget = new LythonMemoryBudget(Math.Max(512 * 1024, MemoryLimit + 65536));
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import csv\nok = False\ntry:\n    rows = list(csv.reader('a,b,c,d,e,f\\n' for i in range(" + SmallRows + ")))\nexcept MemoryError:\n    ok = True\nreturn ok\n", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(MemoryLimit) };
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var result = attempt == 0 ? script.Run(new MockLythonHost(), options)
                : await script.RunAsync(new MockLythonHost(), options);
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task HostBatchWorkingSetStaysBoundedAcrossTotalSizes()
    {
        var small = await ScanBatches(SmallRows);
        var large = await ScanBatches(LargeRows);
        // The same batch size has the same admitted working set. The cap and
        // cleanup-to-zero assertions in ScanBatches are the primary witnesses.
        Assert.Equal(small, large);
    }

    private async Task<long> ScanBatches(int rows, bool file = false)
    {
        var limit = Math.Max(512 * 1024, MemoryLimit + 65536);
        var budget = new LythonMemoryBudget(limit);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(MemoryLimit) };
        var completed = 0;
        long peak = 0;
        for (var offset = 0; offset < rows; offset += BatchRows)
        {
            var count = Math.Min(BatchRows, rows - offset);
            var source = file
                ? "import csv\nwith open('/r.csv') as stream:\n    total = 0\n    for row in csv.reader(stream):\n        total += 1\nreturn total\n"
                : ScanSource(count);
            using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var syncHost = new MockLythonHost();
            var asyncHost = new DelayedLythonHost();
            if (file)
            {
                var content = string.Concat(Enumerable.Repeat("a,b,c,d,e,f\n", count));
                syncHost.SeedFile("/r.csv", content);
                asyncHost.SeedFile("/r.csv", content);
            }
            using (var result = script.Run(syncHost, options)) Verify(result);
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(asyncHost, options)) Verify(result);
            if (file) Assert.True(asyncHost.CompletedAsynchronously > 0);
            Assert.Equal(baseline, budget.ReservedBytes);
            script.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
            completed += count;

            void Verify(LythonExecutionResult result)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(count), result.ReturnValue);
                Assert.InRange(result.PeakExecutionMemoryBytes, 1, MemoryLimit);
                peak = Math.Max(peak, result.PeakExecutionMemoryBytes);
            }
        }
        Assert.Equal(rows, completed);
        Assert.InRange(budget.PeakBytes, 1, limit);
        return peak;
    }
}
