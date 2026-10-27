using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecimalContextLifetimeTests
{
    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NumericIngress", "context-lifetimes.json");
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!];
    }

    [Theory, MemberData(nameof(Cases))]
    public async Task IdentityAndCopiesMatchIndependentCpython(string name, string source, string output)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(output, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task MutableTemplatesAreIsolatedAcrossExecutions()
    {
        using var script = new LythonEngine().Compile("""
            import decimal as d
            assert d.DefaultContext.prec == 28
            assert d.BasicContext.prec == 9
            assert d.ExtendedContext.prec == 9
            assert not d.DefaultContext.flags.get(d.Inexact, False)
            d.DefaultContext.prec = 3
            d.BasicContext.prec = 4
            d.ExtendedContext.prec = 5
            d.DefaultContext.flags[d.Inexact] = True
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        for (var i = 0; i < 3; i++)
        {
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var sync = script.Run(new MockLythonHost(), options)) Assert.True(sync.Success, sync.Failure?.Message);
            using (var asyncResult = await script.RunAsync(new MockLythonHost(), options)) Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
