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

    [Fact]
    public async Task CancelledScanFailsDeterministically()
    {
        // MG01/MG02: cancellation surfaces mid-scan as an explicit failure,
        // never as budget-shaped or CLR leakage; a pre-cancelled token fails
        // before any read. Pulls check the budget (and its token) per row.
        var content = new StringBuilder();
        for (var i = 0; i < 200000; i++)
        {
            content.Append("a,b,c,d\n");
        }

        var script = new LythonEngine().Compile("""
            import csv
            with open("/data.csv") as f:
                n = 0
                for row in csv.reader(f):
                    n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        using var cts = new CancellationTokenSource();
        var host = new MockLythonHost();
        host.SeedFile("/data.csv", content.ToString());
        var runTask = Task.Run(() => script.Run(host, new LythonRunOptions { CancellationToken = cts.Token }));
        await Task.Delay(25);
        cts.Cancel();
        var result = await runTask;
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);

        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();
        var preHost = new MockLythonHost();
        preHost.SeedFile("/data.csv", content.ToString());
        var asyncResult = await script.RunAsync(
            preHost,
            new LythonRunOptions { CancellationToken = preCancelled.Token });
        Assert.False(asyncResult.Success);
        Assert.NotNull(asyncResult.Failure);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("execution canceled", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }
}
