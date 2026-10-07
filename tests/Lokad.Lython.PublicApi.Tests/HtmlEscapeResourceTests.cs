using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HtmlEscapeResourceTests
{
    [Fact]
    public async Task EscapeUsesScalarStringLimitsAndPrivateProcessing()
    {
        await AssertBothModes("import html\nprint(html.escape('😀<'))",
            new LythonRunOptions { MaxStringLength = 5, MaxCollectionSize = 1 }, "😀&lt;\n");
    }

    [Fact]
    public async Task DeniedBasicExpansionStopsBeforeQuoteTruth()
    {
        await AssertBothModes("""
            import html
            class Quote:
                def __bool__(self):
                    print('q')
                    return True
            try:
                html.escape('<',Quote())
            except RuntimeError:
                print('err')
            """, new LythonRunOptions { MaxStringLength = 3 }, "err\n");
    }

    private static async Task AssertBothModes(string source, LythonRunOptions options, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}
