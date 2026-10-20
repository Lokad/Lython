using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonIndentAdmissionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task IndentationUsesStorageAdmissionInsteadOfAnArbitraryWidthLimit(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import json
            for indent in [33, 64, '\t🙂', -3, True]:
                for value in [[], [1]]:
                    print(json.dumps(value,indent=indent)==json.JSONEncoder(indent=indent).encode(value))
            return [json.dumps([1],indent=33), json.dumps([],indent=33), json.dumps([1],indent=-3)]
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(65536);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(string.Concat(Enumerable.Repeat("True\n", 10)), result.StandardOutput);
            Assert.Equal(new object?[] { "[\n" + new string(' ', 33) + "1\n]", "[]", "[\n1\n]" }, Assert.IsAssignableFrom<IReadOnlyList<object?>>(result.ReturnValue));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ConstructorValidatesWithoutMaterializingUnusedIndentation(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("import json\nencoder=json.JSONEncoder(indent=100000)\nreturn encoder.indent");
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new System.Numerics.BigInteger(100000), result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
