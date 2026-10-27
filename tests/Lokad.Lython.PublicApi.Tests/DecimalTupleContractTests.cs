using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DecimalTupleContractTests
{
    private static IEnumerable<object[]> ReadCases(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NumericIngress", file);
        foreach (var row in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [row.GetProperty("name").GetString()!, row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!];
    }
    public static IEnumerable<object[]> PureCases() => ReadCases("tuple-cases.json");
    public static IEnumerable<object[]> HostedCases() => ReadCases("tuple-hosted.json");

    [Theory, MemberData(nameof(PureCases))]
    public async Task TupleBehaviorMatchesIndependentCpython(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                       : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                Assert.Empty(result.StandardError);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory, MemberData(nameof(HostedCases))]
    public async Task DigitConstructionAndNativeIndexingActuallySuspend(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var immediate = new MockLythonHost("/repo"); immediate.SeedFile("/repo/input.txt", "123");
            var delayed = new DelayedLythonHost("/repo"); delayed.SeedFile("/repo/input.txt", "123");
            using (var result = mode == "sync" ? script.Run(immediate, new LythonRunOptions { MemoryBudget = budget })
                       : await script.RunAsync(mode == "delayed" ? delayed : immediate, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                Assert.Empty(result.StandardError);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task NativeTupleProjectsThroughTheOwnedSequenceBoundary()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("from decimal import Decimal\nreturn Decimal('12.3').as_tuple()", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var values = Assert.IsType<object?[]>(result.ReturnValue);
                    Assert.Equal(new BigInteger(0), values[0]);
                    Assert.Equal(new object?[] { new BigInteger(1), new BigInteger(2), new BigInteger(3) }, Assert.IsType<object?[]>(values[1]));
                    Assert.Equal(new BigInteger(-1), values[2]);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
