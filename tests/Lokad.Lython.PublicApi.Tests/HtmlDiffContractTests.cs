using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HtmlDiffContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TablePreservesCompleteEscapedMarkup(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import difflib
            return difflib.HtmlDiff(2, None, None, None).make_table(["a\tb<&é😀\n"], ["a\tb<&é😀\n"])
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        const string cell = "a&nbsp;b&lt;&amp;&#233;&#128512;";
        Assert.Equal("<table class=\"diff\" summary=\"Differences\">\n<tbody>\n"
            + "<tr><td class=\"diff_next\"></td><td class=\"diff_header\">1</td><td nowrap=\"nowrap\">" + cell
            + "</td><td class=\"diff_next\"></td><td class=\"diff_header\">1</td><td nowrap=\"nowrap\">" + cell
            + "</td></tr>\n</tbody>\n</table>", result.ReturnValue);
    }

    [Theory]
    [InlineData("wrapcolumn=3", "wrapcolumn must be None")]
    [InlineData("charjunk=difflib.IS_CHARACTER_JUNK", "charjunk must be None")]
    public void KnownUnsupportedOptionsFailCompilation(string options, string message)
    {
        using var script = new LythonEngine().Compile("import difflib\nreturn difflib.HtmlDiff(" + options + ")");
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Message.Contains(message, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DynamicUnsupportedOptionsFailExplicitlyWithoutCallingPredicate(bool asynchronous, bool predicate)
    {
        var source = "import difflib\ndef configure(value):\n    return difflib.HtmlDiff("
            + (predicate ? "charjunk=value" : "wrapcolumn=value")
            + ")\ndef junk(c):\n    print('called')\n    return False\nconfigure("
            + (predicate ? "junk" : "3") + ")\n";
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.False(result.Success);
        Assert.Equal("NotImplementedError", result.Failure?.ExceptionType);
        Assert.Contains(predicate ? "charjunk must be None" : "wrapcolumn must be None", result.Failure!.Message);
        Assert.Empty(result.StandardOutput);
    }
}
