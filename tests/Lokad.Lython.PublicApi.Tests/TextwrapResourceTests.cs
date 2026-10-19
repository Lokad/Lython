using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextwrapResourceTests
{

    [Fact]
    public async Task AnUnchangedDedentKeepsAliasesUsable()
    {
        await AssertBothModes("""
            import textwrap
            text='a😀\nb'
            for i in range(100):
                assert textwrap.dedent(text) is text
            print(text)
            """, new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) }, "a😀\nb\n");
    }

    private static async Task AssertBothModes(string source, LythonRunOptions options, string expected)
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
