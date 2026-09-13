using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: records parse one at a time and stream past without retention, so
/// a consumer that stops early never pays for the tail and a full scan only
/// carries the current record. Construction accepts the source without
/// pulling, and line_num tracks consumed input.
/// </summary>
public sealed class CsvIncrementalParsingScenarioTests
{
    private static MockLythonHost SeededDataHost()
    {
        var content = new StringBuilder();
        for (var i = 0; i < 12000; i++)
        {
            content.Append("a,b,c,d\n");
        }

        var host = new MockLythonHost();
        host.SeedFile("/data.csv", content.ToString());
        return host;
    }

    [Fact]
    public async Task EarlyBreakAvoidsTailParse()
    {
        // A full parse retains ~6MB; the first row plus the file fits in 1MB.
        var script = new LythonEngine().Compile("""
            import csv
            f = open("/data.csv")
            r = csv.reader(f)
            for row in r:
                return row[0]
            return None
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(SeededDataHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a", sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededDataHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task EarlyBreakAvoidsTailParseDictReader()
    {
        var script = new LythonEngine().Compile("""
            import csv
            f = open("/data.csv")
            r = csv.DictReader(f, ["a", "b", "c", "d"])
            for row in r:
                return row["a"]
            return None
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(SeededDataHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a", sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededDataHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ConstructionAcceptsBrokenInputWithZeroLineNum()
    {
        var script = new LythonEngine().Compile("""
            import csv
            r = csv.reader(["\"broken"])
            return r.line_num
            """);
        Assert.True(script.IsValid);
        var expected = new BigInteger(0);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task LineNumProgressesWithPulls()
    {
        // Readers stream single-pass; iteration is the incremental path and
        // line_num tracks pulled input.
        var script = new LythonEngine().Compile("""
            import csv
            r = csv.reader(["a,b", "1,2", "3,4"])
            seen = []
            for row in r:
                seen.append(r.line_num)
            return seen
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(3) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BreakThenResumeSameReader()
    {
        // Breaking out early must not consume or poison the shared cursor: a
        // later pass over the same reader continues where the break left off,
        // over list and file sources alike.
        var script = new LythonEngine().Compile("""
            import csv
            r = csv.reader(["a", "b", "c", "d"])
            first = []
            for row in r:
                first.append(row[0])
                if len(first) == 2:
                    break
            second = [row[0] for row in r]
            f = open("/data.csv")
            fr = csv.reader(f)
            ffirst = []
            for row in fr:
                ffirst.append(row[0])
                if len(ffirst) == 2:
                    break
            fsecond = [row[0] for row in fr]
            return [first, second, ffirst, fsecond]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { "a", "b" },
            new List<object?> { "c", "d" },
            new List<object?> { "a", "b" },
            new List<object?> { "c", "d" },
        };

        var host = new MockLythonHost();
        host.SeedFile("/data.csv", "a\nb\nc\nd\n");
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var host2 = new MockLythonHost();
        host2.SeedFile("/data.csv", "a\nb\nc\nd\n");
        var asyncResult = await script.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
