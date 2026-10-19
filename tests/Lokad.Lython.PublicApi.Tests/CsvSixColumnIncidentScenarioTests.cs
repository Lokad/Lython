using System.Numerics;
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            rows = ["a,b,c,d,e,f"] * 12000
            r = csv.DictReader(rows)
            out = []
            for row in r:
                out.append(row)
            return len(out)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(SixColumnBudgetBytes) };
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
    public async Task FlatDictBatchesPreserveTheCompleteIncidentTotal()
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            rows = ["a,b,c,d,e,f"] * (count + 1)
            n = 0
            for row in csv.DictReader(rows):
                n += len(row)
            return n
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var total = 0;
            for (var offset = 0; offset < 29999; offset += 32)
            {
                var count = Math.Min(32, 29999 - offset);
                var options = new LythonRunOptions
                {
                    MemoryBudget = new LythonMemoryBudget(131072),
                    Globals = new Dictionary<string, object?> { ["count"] = count }
                };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(count * 6), result.ReturnValue);
                    Assert.InRange(result.PeakExecutionMemoryBytes, 1, 131072);
                    total += count * 6;
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(179994, total);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task EarlyBreakOverSixColumnsFitsSameBudget()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import csv
            rows = ["a,b,c,d,e,f"] * 12000
            r = csv.DictReader(rows)
            for row in r:
                return row["a"]
            return None
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(SixColumnBudgetBytes) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a", sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a", asyncResult.ReturnValue);
    }
}
