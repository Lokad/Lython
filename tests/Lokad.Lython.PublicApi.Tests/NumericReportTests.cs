using System.Text;
using Lokad.Lython.Benchmarks.Comparison;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NumericReportTests
{
    public static IEnumerable<object[]> Cases()
        => WorkloadCatalog.CreateRepresentative().Where(c => c.Id.StartsWith("scripts.numeric-report.", StringComparison.Ordinal)
            || c.Id.StartsWith("scripts.csv-invoice.", StringComparison.Ordinal))
            .Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NumericReportKeepsCompleteFilesAndReturnsTheSharedBudget(string id)
    {
        var workload = WorkloadCatalog.CreateRepresentative().Single(c => c.Id == id);
        var fixture = ComparisonFileFixture.Parse(workload.FixtureJson)!;
        // CSV fields and per-record tuples stay conservatively owned by this
        // finite command; its 4,096-row case needs more than the JSON arrays.
        var bytes = workload.Id.StartsWith("scripts.csv-invoice.", StringComparison.Ordinal)
            ? 16 * 1024 * 1024 : 4 * 1024 * 1024;
        var budget = new LythonMemoryBudget(bytes);
        using (var script = new LythonEngine().Compile(workload.Source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var host = new MockLythonHost();
                foreach (var input in fixture.Inputs) host.SeedFile("/" + input.Key, Encoding.UTF8.GetString(input.Value));
                using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(workload.ExpectedOutput, result.StandardOutput);
                    foreach (var output in fixture.Outputs)
                        Assert.Equal(Encoding.UTF8.GetString(output.Value), host.ReadText("/" + output.Key));
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
