using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: CSV field payload is retained in every row, so decoded fields charge
/// the execution budget at parse time instead of arriving free.
/// </summary>
public sealed class CsvFieldAccountingScenarioTests
{
    [Fact]
    public async Task ManySmallFieldsStayCharged()
    {
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
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var host2 = new MockLythonHost();
        host2.SeedFile("/data.csv", content.ToString());
        var asyncResult = await script.RunAsync(host2, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
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