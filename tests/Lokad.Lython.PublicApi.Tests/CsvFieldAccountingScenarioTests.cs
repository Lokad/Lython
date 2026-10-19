using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CsvFieldAccountingScenarioTests
{
    [Fact]
    public Task FieldBatchesReturnSharedAccounting()
        => VerifyFieldBatches(512);

    [Fact]
    [Trait("Category", "Stress")]
    public Task IncidentScaleFieldBatchesReturnSharedAccounting()
        => VerifyFieldBatches(100000);

    private static async Task VerifyFieldBatches(int rows)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            with open('/data.csv') as stream:
                n = 0
                for row in csv.reader(stream):
                    assert row == ['a', 'b', 'c', 'd']
                    n += 1
            return n
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        foreach (var asynchronous in new[] { false, true })
        {
            var completed = 0;
            for (var offset = 0; offset < rows; offset += 32)
            {
                var count = Math.Min(32, rows - offset);
                var host = new MockLythonHost();
                host.SeedFile("/data.csv", string.Concat(Enumerable.Repeat("a,b,c,d\n", count)));
                using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(count), result.ReturnValue);
                    Assert.InRange(result.PeakExecutionMemoryBytes, 1, 65536);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
                completed += count;
            }
            Assert.Equal(rows, completed);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RetainedFieldResultsStayCorrectAcrossHostBatches()
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            with open('/data.csv') as stream:
                kept = None
                n = 0
                for row in csv.reader(stream):
                    if kept is None:
                        kept = row[1]
                    n += 1
            return [n, kept]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(8388608) };
        foreach (var asynchronous in new[] { false, true })
        {
            var retained = new List<LythonExecutionResult>();
            var completed = 0;
            try
            {
                for (var batch = 0; batch < 120; batch++)
                {
                    var host = new MockLythonHost();
                    host.SeedFile("/data.csv", string.Concat(Enumerable.Repeat("a,b,c,d\n", 100)));
                    var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
                    retained.Add(result);
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new List<object?> { new BigInteger(100), "b" }, result.ReturnValue);
                    completed += 100;
                }
                Assert.Equal(12000, completed);
                Assert.Equal(120, retained.Count);
                foreach (var result in retained)
                    Assert.Equal(new List<object?> { new BigInteger(100), "b" }, result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
            }
            finally { foreach (var result in retained) result.Dispose(); }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task WideRowScratchTripsBeforeUnchargedGrowth()
    {
        // 200001 empty fields commit nothing per field but need ~1.6MB of row
        // scratch while accumulating; the geometric reservation trips 4MB
        // before the growth runs uncharged, while the 3.2MB retained backing
        // alone would fit.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            line = "," * 200000
            r = csv.reader([line])
            return len(list(r)[0])
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4194304) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task FundedWideRowStillParses()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            line = "," * 200000
            r = csv.reader([line])
            return len(list(r)[0])
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(12582912) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(200001), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(200001), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SmallCsvStillParses()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            import csv
            rows = csv.reader(["a,b", "c,d"])
            out = []
            for row in rows:
                out.append(row[1])
            return out
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "b", "d" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledScanFailsDeterministically(bool asynchronous)
    {
        // Cancel after a known record, independently of thread-pool/timer
        // scheduling. The remaining records must never complete the scan.
        var content = string.Concat(Enumerable.Repeat("a,b,c,d\n", 512));
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            with open("/data.csv") as f:
                n = 0
                for row in csv.reader(f):
                    n = n + 1
                    if n == 100:
                        print("checkpoint")
            return n
            """);
        Assert.True(script.IsValid);
        using var cancellation = new CancellationTokenSource();
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        ILythonHost host = (ILythonHost?)delayed ?? mock!;
        var writes = 0;
        void CancelAtCheckpoint()
        {
            // print writes the text and newline separately. Cancel after the
            // full marker has reached the host and retained output capture.
            if (++writes == 2) cancellation.Cancel();
        }
        if (delayed is not null)
        {
            delayed.SeedFile("/data.csv", content);
            delayed.OnStandardOutputWrite = CancelAtCheckpoint;
        }
        else
        {
            mock!.SeedFile("/data.csv", content);
            mock.OnStandardOutputWrite = CancelAtCheckpoint;
        }
        var options = new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
            MemoryBudget = new LythonMemoryBudget(262144),
        };
        var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
        if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(2, writes);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        Assert.Equal("checkpoint\n", result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 262144);

        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();
        var preHost = new MockLythonHost(); // no file: cancellation precedes host access
        var preOptions = new LythonRunOptions { CancellationToken = preCancelled.Token };
        var preResult = asynchronous ? await script.RunAsync(preHost, preOptions) : script.Run(preHost, preOptions);
        Assert.False(preResult.Success);
        Assert.Equal("RuntimeError", preResult.Failure?.ExceptionType);
        Assert.Equal(LythonFailureReason.Cancelled, preResult.FailureAttribution?.Reason);
        Assert.Empty(preResult.StandardOutput);
    }
}
