using System.Text;
using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneralIntegerTextTests
{
    public static IEnumerable<object[]> Programs()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "IntegerParsing", "cases.json");
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path, Encoding.UTF8))!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!];
    }

    [Theory, MemberData(nameof(Programs))]
    public async Task GeneralInputsMatchPython(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var options = new LythonRunOptions { MemoryBudget = budget, ResultMemoryBudget = budget };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
