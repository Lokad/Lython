using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StatisticsContractTests
{
    private static IEnumerable<object[]> Read(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "StatisticsContracts", file);
        foreach (var item in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [item.GetProperty("name").GetString()!, item.GetProperty("source").GetString()!, item.GetProperty("output").GetString()!];
    }

    public static IEnumerable<object[]> Moments() => Read("moments.json");
    public static IEnumerable<object[]> Records() => Read("records.json");
    public static IEnumerable<object[]> Hosted() => Read("hosted.json");

    [Theory, MemberData(nameof(Moments)), MemberData(nameof(Records))]
    public async Task StatisticalResultsMatchIndependentPythonGoldens(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(1048576);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory, MemberData(nameof(Hosted))]
    public async Task RecordProtocolsActuallyAwaitIO(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(1048576);
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var host = new MockLythonHost(); host.SeedFile("value.txt", "1");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("value.txt", "1");
            using (var result = mode == "sync" ? script.Run(host, new LythonRunOptions { MemoryBudget = budget })
                : await script.RunAsync(mode == "delayed" ? delayed : host, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task SampleAcquisitionActuallySuspendsAndReturnsItsOwner()
    {
        const string source = """
            import statistics
            def data():
                yield 10000000000000000
                with open('/value') as f:
                    yield 10000000000000000+int(f.read())
                yield 10000000000000002
            d=statistics.NormalDist.from_samples(data())
            return [d.stdev,'x'*8192]
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(1048576);
        var host = new DelayedLythonHost(); host.SeedFile("/value", "1");
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            var values = Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue);
            Assert.Equal(1.0, values[0]);
            Assert.Equal(new string('x', 8192), values[1]);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
