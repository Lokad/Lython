using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CopyContractTests
{
    private static IEnumerable<object[]> Read(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CopyContracts", file);
        foreach (var item in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!)
            yield return [item.GetProperty("name").GetString()!, item.GetProperty("source").GetString()!, item.GetProperty("output").GetString()!];
    }

    public static IEnumerable<object[]> PureCases() => Read("pure.json");
    public static IEnumerable<object[]> HostedCases() => Read("hosted.json");

    [Theory, MemberData(nameof(PureCases))]
    public async Task CopyProtocolsMatchIndependentPythonGoldens(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(1048576);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory, MemberData(nameof(HostedCases))]
    public async Task CopyHooksActuallySuspendAndReturnTheirOwners(string name, string source, string output)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(1048576);
        foreach (var mode in new[] { "sync", "immediate", "delayed" })
        {
            var immediate = new MockLythonHost(); immediate.SeedFile("/input", "alpha");
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/input", "alpha");
            using (var result = mode == "sync" ? script.Run(immediate, new LythonRunOptions { MemoryBudget = budget })
                : await script.RunAsync(mode == "delayed" ? delayed : immediate, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(output, result.StandardOutput);
                if (mode == "delayed") Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task UnsupportedReductionRegistryRejectsStaticAndDynamicAccess()
    {
        using var invalid = new LythonEngine().Compile("import copy\ncopy.dispatch_table[object]=str");
        Assert.False(invalid.IsValid);
        using var script = new LythonEngine().Compile("import copy\ntry:getattr(copy,'dispatch_table')\nexcept AttributeError:print('unsupported')\nprint(copy.Error is copy.error)");
        Assert.True(script.IsValid);
        using var result = await script.RunAsync(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("unsupported\nTrue\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task EscapingMemoAndCopiedResultsRetainFundingUntilRelinquished(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import copy
            retained=[]
            class Hook:
                def __deepcopy__(self,memo):retained.append(memo);return 'value'
            source=[b'x'*16384,Hook()]
            copied=copy.deepcopy(source)
            assert retained[0][id(source)] is copied
            return copied
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(262144);
        for (var batch = 0; batch < 3; batch++)
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.True(budget.ReservedBytes > 16384);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
