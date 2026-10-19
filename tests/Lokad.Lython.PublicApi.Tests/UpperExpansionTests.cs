using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UpperExpansionTests
{
    [Theory]
    [InlineData("\u00df", "SS", "Ss")]
    [InlineData("\u0149", "\u02bcN", "\u02bcN")]
    [InlineData("\u01f0", "J\u030c", "J\u030c")]
    [InlineData("\u0390", "\u0399\u0308\u0301", "\u0399\u0308\u0301")]
    [InlineData("\u03b0", "\u03a5\u0308\u0301", "\u03a5\u0308\u0301")]
    [InlineData("\u0587", "\u0535\u0552", "\u0535\u0582")]
    [InlineData("\ufb00", "FF", "Ff")]
    [InlineData("\ufb01", "FI", "Fi")]
    [InlineData("\ufb02", "FL", "Fl")]
    [InlineData("\ufb03", "FFI", "Ffi")]
    [InlineData("\ufb04", "FFL", "Ffl")]
    [InlineData("\ufb05", "ST", "St")]
    [InlineData("\ufb06", "ST", "St")]
    [InlineData("\ufb13", "\u0544\u0546", "\u0544\u0576")]
    [InlineData("\ufb14", "\u0544\u0535", "\u0544\u0565")]
    [InlineData("\ufb15", "\u0544\u053b", "\u0544\u056b")]
    [InlineData("\ufb16", "\u054e\u0546", "\u054e\u0576")]
    [InlineData("\ufb17", "\u0544\u053d", "\u0544\u056d")]
    public async Task ExistingExpansionFamiliesKeepUpperAndLegacyCasing(string text, string upper, string title)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return [text.upper(), text.title(), text.capitalize(), text.swapcase()]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new MockLythonHost();
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = text } };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new object?[] { upper, title, title, upper }, Assert.IsType<List<object?>>(result.ReturnValue));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
