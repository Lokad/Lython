using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: records parse one at a time and accumulate in a row cache, so a
/// consumer that stops early never pays for the tail. Construction accepts
/// the source without pulling, and line_num tracks consumed input.
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
        // Indexing normalizes through Length and completes the parse, like
        // len(); iteration is the incremental path.
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
}