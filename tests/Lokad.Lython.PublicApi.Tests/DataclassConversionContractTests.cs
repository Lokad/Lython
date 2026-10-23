using Lokad.Lython.Tests.Harness;
using System.Text.Json;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DataclassConversionContractTests
{
    public static IEnumerable<object[]> Cases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "DataclassCorpus", "cases.json")));
        foreach (var item in document.RootElement.GetProperty("cases").EnumerateArray())
            yield return [item.GetProperty("name").GetString()!, item.GetProperty("source").GetString()!, item.GetProperty("output").GetString()!];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesIndependentPythonCases(string name, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join(";", script.Diagnostics.Select(d => d.Message)));
        var budget = new LythonMemoryBudget(512 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }) : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            { Assert.True(result.Success, name + ": " + result.Failure?.Message); Assert.Equal(expected, result.StandardOutput); }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
