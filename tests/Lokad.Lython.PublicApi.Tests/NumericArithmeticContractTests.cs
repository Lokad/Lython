using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NumericArithmeticContractTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NumericArithmetic", "cases.json");
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!];
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task NumericResultsMatchIndependentCpython(string name, string source, string output)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(output, result.StandardOutput);
                    Assert.Empty(result.StandardError);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
