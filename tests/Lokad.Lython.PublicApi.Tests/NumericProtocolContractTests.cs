using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NumericProtocolContractTests
{
    private static IEnumerable<object[]> ReadCases(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NumericProtocols", file);
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!];
    }

    public static IEnumerable<object[]> PureCases() => ReadCases("cases.json");
    public static IEnumerable<object[]> HostedCases() => ReadCases("hosted.json");

    [Theory, MemberData(nameof(PureCases))]
    public async Task ImplicitProtocolsMatchIndependentCpython(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                Assert.Empty(result.StandardError);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory, MemberData(nameof(HostedCases))]
    public async Task RealProtocolWorkSuspendsAndCleansUp(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var immediate = new MockLythonHost("/repo"); immediate.SeedFile("/repo/input.txt", "ok");
            var delayed = new DelayedLythonHost("/repo"); delayed.SeedFile("/repo/input.txt", "ok");
            using (var result = mode == "sync" ? script.Run(immediate, new LythonRunOptions { MemoryBudget = budget })
                       : await script.RunAsync(mode == "delayed" ? delayed : immediate, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                Assert.Empty(result.StandardError);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
