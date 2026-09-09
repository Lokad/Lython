using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: DictReader converts rows to dictionaries on demand instead of
/// materializing every dictionary up front, so early termination only pays
/// for consumed rows on top of the parsed records.
/// </summary>
public sealed class CsvDictReaderLazinessScenarioTests
{
    [Fact]
    public async Task DictReaderBuildsDictsOnDemand()
    {
        // 20k aliased single-column rows share one field payload; the first
        // row breaks out before any other dictionary exists.
        var script = new LythonEngine().Compile(
            """
            import csv
            rows = ["a"] * 20000
            r = csv.DictReader(rows, ["k"])
            for row in r:
                return row["k"]
            return None
            """);
        Assert.True(script.IsValid);
        // Rows, fields and input fit under 9MiB; the eager dictionaries do not.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 9437184 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DictReaderLazyBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import csv
            r = csv.DictReader(["a,b", "1,2", "3"], restval="0")
            first = r[0]
            second = r[1]
            return [first["a"], first["b"], second["a"], second["b"], r.fieldnames, len(r), r.line_num, len(list(csv.DictReader([]))), len(r[0:2])]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "1", "2", "3", "0",
            new List<object?> { "a", "b" },
            new BigInteger(2), new BigInteger(3), new BigInteger(0), new BigInteger(2),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}