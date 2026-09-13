using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01/MG21: CSV readers stream through the async host boundary in async runs,
/// so genuinely-delayed hosts suspend per pull instead of failing the synchronous
/// capability check. Values match synchronous runs exactly.
/// </summary>
public sealed class CsvDelayedHostTests
{
    private static string RowCsv(int rows)
    {
        var content = new StringBuilder("id,pad\n");
        for (var i = 0; i < rows; i++)
        {
            content.Append(i).Append(',').Append('y', 50).Append('\n');
        }

        return content.ToString();
    }

    [Fact]
    public async Task DelayedAsyncCsvReaderStreamsRows()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/r.csv") as f:
                total = 0
                n = 0
                for row in csv.reader(f):
                    if n > 0:
                        total = total + int(row[0])
                    n = n + 1
            return [n, total]
            """);
        Assert.True(script.IsValid);

        var host = new DelayedLythonHost();
        host.SeedFile("/r.csv", RowCsv(2000));
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(2001), new BigInteger(1999000) }, asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task DelayedAsyncDictReaderHeaderPaths()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/r.csv") as f:
                inferred = [row for row in csv.DictReader(f)]
            with open("/r.csv") as f:
                explicit = [row for row in csv.DictReader(f, ["a", "b"])]
                assert inferred[0] == {"id": "0", "pad": "y" * 50}, repr(inferred[0])
                assert inferred[1999]["id"] == "1999", repr(inferred[1999])
                assert explicit[0] == {"a": "id", "b": "pad"}, repr(explicit[0])
                assert explicit[1] == {"a": "0", "b": "y" * 50}, repr(explicit[1])
            return [len(inferred), len(explicit)]
            """);
        Assert.True(script.IsValid);

        var host = new DelayedLythonHost();
        host.SeedFile("/r.csv", RowCsv(2000));
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(2000), new BigInteger(2001) }, asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task DelayedAsyncPartialAndMaterialized()
    {
        // next() over csv readers is a separate protocol gap (readers are not
        // iterators in Lython yet); partial consumption plus list() covers the
        // shared-position contract through the async boundary instead.
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/r.csv") as f:
                reader = csv.reader(f)
                first_two = []
                for row in reader:
                    first_two.append(row)
                    if len(first_two) == 2:
                        break
                rest = list(reader)
                assert first_two[0] == ["id", "pad"], repr(first_two[0])
                assert first_two[1] == ["0", "y" * 50], repr(first_two[1][:1])
                assert rest[0] == ["1", "y" * 50], repr(rest[0][:1])
            return len(rest)
            """);
        Assert.True(script.IsValid);

        var host = new DelayedLythonHost();
        host.SeedFile("/r.csv", RowCsv(2000));
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(1999), asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public void SyncCsvRunOnDelayedHostGuidesToRunAsync()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/r.csv") as f:
                return len(list(csv.reader(f)))
            """);
        Assert.True(script.IsValid);

        var host = new DelayedLythonHost();
        host.SeedFile("/r.csv", RowCsv(10));
        var sync = script.Run(host);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("use RunAsync", sync.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DelayedAsyncMultilineQuotes()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/q.csv") as f:
                rows = list(csv.reader(f))
                assert rows[0] == ["a", "b"], repr(rows[0])
                assert rows[1] == ["x\ny", "1"], repr(rows[1])
                assert rows[2] == ["p,q", " ended "], repr(rows[2])
            return len(rows)
            """);
        Assert.True(script.IsValid);

        var host = new DelayedLythonHost();
        host.SeedFile("/q.csv", "a,b\n\"x\ny\",1\n\"p,q\",\" ended \"\n");
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(3), asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task DelayedAsyncCsvWriterRoundTrips()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/w.csv", "w") as f:
                writer = csv.writer(f)
                writer.writerow(["a", "b"])
                writer.writerows([["1", "x"], ["2", "y"]])
            with open("/w.csv") as f:
                return f.read()
            """);
        Assert.True(script.IsValid);

        var host = new DelayedLythonHost();
        var asyncResult = await script.RunAsync(host);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a,b\n1,x\n2,y\n", asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task DelayedAsyncBoundedScan()
    {
        // The incident shape through a genuinely-delayed host: 100k rows
        // (~5.7MB) aggregate under the same 8MB budget as the instant-host
        // bound pin, which whole-file acquisition (~24MB+) could never fit.
        // Suspension time between pulls also paces collection, unlike the
        // adversarial instant-host case, so this mirrors that pin exactly.
        var script = new LythonEngine().Compile(
            """
            import csv
            with open("/big.csv") as f:
                n = 0
                for row in csv.reader(f):
                    n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 8388608 };

        var host = new DelayedLythonHost();
        host.SeedFile("/big.csv", RowCsv(100000));
        var asyncResult = await script.RunAsync(host, options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(100001), asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}
