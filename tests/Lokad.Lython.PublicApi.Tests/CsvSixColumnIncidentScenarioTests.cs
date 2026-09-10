using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG01: the incident shape is six-column DictReader input. Parsed rows and
/// field payloads are retained for every record, and each consumed dictionary
/// adds its own backing on top, so a retaining consumer must fail early while
/// early termination still fits the same budget.
/// </summary>
public sealed class CsvSixColumnIncidentScenarioTests
{
    // 12k six-column records parse to ~12.18MB; the 12k consumed dictionaries
    // add ~2.6MB on top (~14.78MB retained). The budget sits between the two.
    private const long SixColumnBudgetBytes = 13500000;

    [Fact]
    public async Task RetainingSixColumnDictsTripsBudgetEarly()
    {
        var script = new LythonEngine().Compile("""
            import csv
            rows = ["a,b,c,d,e,f"] * 12000
            r = csv.DictReader(rows)
            out = []
            for row in r:
                out.append(row)
            return len(out)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SixColumnBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= SixColumnBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= SixColumnBudgetBytes);
    }

    [Fact]
    public async Task EarlyBreakOverSixColumnsFitsSameBudget()
    {
        var script = new LythonEngine().Compile("""
            import csv
            rows = ["a,b,c,d,e,f"] * 12000
            r = csv.DictReader(rows)
            for row in r:
                return row["a"]
            return None
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SixColumnBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a", asyncResult.ReturnValue);
    }
}