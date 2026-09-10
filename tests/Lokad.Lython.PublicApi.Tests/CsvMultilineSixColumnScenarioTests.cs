using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: long quoted fields spanning physical lines compose with multi-column
/// records. Each record retains its decoded fields while the builder transient
/// covers only the largest single field, so a retaining consumer still fails
/// early and funded runs show no per-record transient accumulation.
/// </summary>
public sealed class CsvMultilineSixColumnScenarioTests
{
    // 400 records x (602-char quoted field + 5 tiny fields) retain ~765508B;
    // the builder transient peaks at 2 x 1024B of capacity and never sums.
    private const long MultilineSixColumnBudgetBytes = 450000;
    private const long MultilineSixColumnPeakBoundBytes = 900000;

    private const string MultilineSixColumnScript = """
        import csv
        la = chr(34) + "A" * 200
        lb = "B" * 200
        lc = "C" * 200 + chr(34) + ",c1,c2,c3,c4,c5"
        lines = ([la, lb, lc] * 400)
        r = csv.DictReader(lines, ["k0", "k1", "k2", "k3", "k4", "k5"])
        out = []
        for row in r:
            out.append(row)
        return len(out)
        """;

    [Fact]
    public async Task MultilineSixColumnRetainTrips()
    {
        var script = new LythonEngine().Compile(MultilineSixColumnScript);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MultilineSixColumnBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= MultilineSixColumnBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= MultilineSixColumnBudgetBytes);
    }

    [Fact]
    public async Task MultilineSixColumnPeakShowsNoTransientAccumulation()
    {
        var script = new LythonEngine().Compile(MultilineSixColumnScript);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 268435456 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(400), sync.ReturnValue);
        Assert.True(sync.PeakExecutionMemoryBytes <= MultilineSixColumnPeakBoundBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(400), asyncResult.ReturnValue);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= MultilineSixColumnPeakBoundBytes);
    }

    [Fact]
    public async Task SmallMultilineSixColumnParsesExactly()
    {
        var script = new LythonEngine().Compile("""
            import csv
            la = chr(34) + "A" * 200
            lb = "B" * 200
            lc = "C" * 200 + chr(34) + ",c1,c2,c3,c4,c5"
            rows = list(csv.reader([la, lb, lc] * 2))
            return rows
            """);
        Assert.True(script.IsValid);
        var field = new string('A', 200) + "\n" + new string('B', 200) + "\n" + new string('C', 200);
        var row = new List<object?> { field, "c1", "c2", "c3", "c4", "c5" };
        var expected = new List<object?> { row, row };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}