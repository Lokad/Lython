using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SharedCompilerFactTests
{
    [Fact]
    public void NestedDeclarationsKeepAllDiagnosticFamiliesAndTheirOrder()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            if False:
                result: int = 'bad'
                def use():
                    return local
                    local = 1
            def outer():
                nonlocal missing
            """, budget))
        {
            Assert.False(script.IsValid);
            var codes = script.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();
            Assert.Contains("LA1080", codes);
            Assert.Contains("LA3205", codes);
            Assert.Contains("LA3146", codes);
            Assert.True(Array.IndexOf(codes, "LA1080") < Array.IndexOf(codes, "LA3205"));
            Assert.True(Array.IndexOf(codes, "LA3205") < Array.IndexOf(codes, "LA3146"));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedImmutableTuplesRemainUsableAcrossADeferredDefinition(bool asynchronous)
    {
        // Forty linear statements describe a shared graph, not 2^40 objects.
        var source = "x = 1\n" + string.Concat(Enumerable.Repeat("x = (x, x)\n", 40)) + """
            def ready():
                return 1
            print(ready())
            for i in range(40):
                x = x[0]
            print(x)
            del x
            """;
        await Check(source, "1\n1\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutableSiblingIsStillInvalidatedAfterSharedImmutableFacts(bool asynchronous)
    {
        var source = "pure = 1\n" + string.Concat(Enumerable.Repeat("pure = (pure, pure)\n", 32)) + """
            items = [0]
            holder = (pure, items)
            def change():
                items.append('ok')
            change()
            print(holder[1][1].upper(), holder[1] is items)
            del pure, holder
            """;
        await Check(source, "OK True\n", asynchronous);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SharedContainerArgumentsKeepNativeCallSemantics(bool dictionary, bool asynchronous)
    {
        var update = dictionary ? "x = {'a': x, 'b': x}\n" : "x = (x, x)\n";
        var source = "x = 1\n" + string.Concat(Enumerable.Repeat(update, 40)) + """
            print(len(x), bool(x))
            del x
            """;
        await Check(source, "2 True\n", asynchronous);
    }

    private static async Task Check(string source, string expected, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
