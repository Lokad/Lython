using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class CsvRetainedRowLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RetainedRowsSurviveReaderCleanupAndReturnFundingWithTheirOwner(bool dictionary, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        var reader = dictionary ? "DictReader" : "reader";
        using var script = new LythonEngine().Compile($$"""
            import csv
            with open('/csv') as file:
                rows = list(csv.{{reader}}(file))
            assert file.closed
            return rows
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var content = "a,b,c,d,e,f\n" + string.Concat(Enumerable.Repeat("1,2,3,4,5,6\n", 16));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        for (var batch = 0; batch < 8; batch++)
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            ILythonHost host = (ILythonHost?)delayed ?? mock!;
            if (delayed is not null) delayed.SeedFile("/csv", content); else mock!.SeedFile("/csv", content);
            using var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            // Physical collection cannot refund an escaping row's live owner.
            if (batch == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.Equal(retained, budget.ReservedBytes);
            var rows = Assert.IsType<List<object?>>(result.ReturnValue);
            Assert.Equal(dictionary ? 16 : 17, rows.Count);
            foreach (var row in rows.Skip(dictionary ? 0 : 1))
            {
                if (dictionary)
                {
                    var values = Assert.IsType<Dictionary<object, object?>>(row);
                    Assert.Equal(6, values.Count);
                    Assert.Equal("1", values["a"]);
                    Assert.Equal("6", values["f"]);
                }
                else Assert.Equal(new List<object?> { "1", "2", "3", "4", "5", "6" }, row);
            }
            // All aliases are relinquished before the result owner is disposed.
            rows = null!;
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
