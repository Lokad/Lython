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
        // 12k four-column rows commit ~6MB of field payload cumulatively; the
        // scan fits 6MB only because dropped fields are reclaimed while the
        // retained row backing stays charged.
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
            rows = csv.reader(f)
            n = 0
            for row in rows:
                n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 };
        var sync = script.Run(host, options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(12000), sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/data.csv", content.ToString());
        var asyncResult = await script.RunAsync(host2, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(12000), asyncResult.ReturnValue);
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
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 };
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
}
