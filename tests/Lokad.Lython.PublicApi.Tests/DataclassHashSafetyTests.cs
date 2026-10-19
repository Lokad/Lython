using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DataclassHashSafetyTests
{
    private const string Node = "from dataclasses import dataclass\n@dataclass(unsafe_hash=True)\nclass Node:\n value:object\n";

    public static IEnumerable<object[]> Cycles()
    {
        foreach (var expression in new[] { "hash(node)", "hash((node,))", "set([node])", "{node:1}" })
        foreach (var asynchronous in new[] { false, true })
            yield return [expression, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Cycles))]
    public async Task CyclicGeneratedHashIsCatchableAndRepairRecovers(string expression, bool asynchronous)
    {
        var source = Node + "for mutual in [False,True]:\n node=Node(None)\n other=Node(node)\n node.value=other if mutual else node\n try:" + expression +
            "\n except RecursionError:print('cycle')\n node.value=7\n print(hash(node)==hash(Node(7)))";
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("cycle\nTrue\ncycle\nTrue\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task DeepHashRespectsManagedBoundsAndTheNextRunRecovers(bool asynchronous, bool tuple)
    {
        var source = "with open('/ready') as stream:stream.read()\n" + Node +
            "node=Node(0)\nfor index in range(depth):node=Node(node)\ntry:print(isinstance(" + (tuple ? "hash((node,))" : "hash(node)") +
            ",int))\nexcept RecursionError:print('bounded')";
        var budget = new LythonMemoryBudget(8 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/ready", "ready");
        var mock = new MockLythonHost(); mock.SeedFile("/ready", "ready");
        foreach (var depth in new[] { 2048, 2 })
        {
            var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["depth"] = depth } };
            using (var result = asynchronous ? await script.RunAsync(delayed, options) : script.Run(mock, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(depth == 2048 ? "bounded\n" : "True\n", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        if (asynchronous) Assert.True(delayed.CompletedAsynchronously > 0);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
