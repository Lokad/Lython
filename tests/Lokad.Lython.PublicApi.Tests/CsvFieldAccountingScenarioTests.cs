using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: CSV field payload charges at parse time, but fields the guest drops
/// are reclaimed as the scan advances, so a full scalar scan only carries the
/// current record plus row backing instead of every field ever parsed.
/// </summary>
public sealed class CsvFieldAccountingScenarioTests
{
    [Fact]
    public async Task FullScanReleasesDroppedFieldCharges()
    {
        // The incident shape at incident scale: 100k four-column rows commit
        // ~51MB of field payload cumulatively, so the scan fits 32MB only
        // because dropped fields (and row backing) are reclaimed while the
        // scalar aggregation retains nothing. Incident-scale margins dwarf
        // GC-paced slack, which smaller budgets could not separate robustly.
        var content = new StringBuilder();
        for (var i = 0; i < 100000; i++)
        {
            content.Append("a,b,c,d\n");
        }

        var host = new MockLythonHost();
        host.SeedFile("/data.csv", content.ToString());
        var script = new LythonEngine().Compile(
            """
            import csv
            f = open("/data.csv")
            rows = csv.reader(f)
            n = 0
            for row in rows:
                n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 33554432 };
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(100000), sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/data.csv", content.ToString());
        var asyncResult = await script.RunAsync(host2, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(100000), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedFieldAliasesStayCorrect()
    {
        // Fields the guest keeps (every 100th second column) stay correct
        // while dropped rows are reclaimed around them.
        var content = new StringBuilder();
        for (var i = 0; i < 12000; i++)
        {
            content.Append("a,b,c,d\n");
        }

        var host = new MockLythonHost();
        host.SeedFile("/data.csv", content.ToString());
        var script = new LythonEngine().Compile(
            """
            import csv
            f = open("/data.csv")
            kept = []
            i = 0
            for row in csv.reader(f):
                if i % 100 == 0:
                    kept.append(row[1])
                i = i + 1
            return [len(kept), kept[0], kept[119], i]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(120), "b", "b", new BigInteger(12000) };
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 8388608 };
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/data.csv", content.ToString());
        var asyncResult = await script.RunAsync(host2, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task WideRowScratchTripsBeforeUnchargedGrowth()
    {
        // 200001 empty fields commit nothing per field but need ~1.6MB of row
        // scratch while accumulating; the geometric reservation trips 4MB
        // before the growth runs uncharged, while the 3.2MB retained backing
        // alone would fit.
        var script = new LythonEngine().Compile("""
            import csv
            line = "," * 200000
            r = csv.reader([line])
            return len(list(r)[0])
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
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
        var script = new LythonEngine().Compile("""
            import csv
            line = "," * 200000
            r = csv.reader([line])
            return len(list(r)[0])
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 12582912 };
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
        var script = new LythonEngine().Compile(
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
        var script = new LythonEngine().Compile("""
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
            MaxExecutionMemoryBytes = 262144,
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
        Assert.Equal(0, preResult.Consumption?.HostCalls);
        Assert.Empty(preResult.StandardOutput);
    }
}
