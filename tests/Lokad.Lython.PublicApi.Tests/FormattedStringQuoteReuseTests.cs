using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormattedStringQuoteReuseTests
{
    [Fact]
    public async Task ExpressionQuotesComposeWithLiteralsAndFormatFields()
    {
        var compiled = new LythonEngine().Compile("""
            d = {'x': 3}
            print(f"{d["x"]}")
            print(f'{d['x']}' ' adjacent')
            print(f"{'a'} {d["x"]:04d} {{literal}}")
            print(f"{"\n".join(['a', 'b'])}")
            print(rf"{d["x"]}\n")
            print('f"{d["x"]}"')
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("3\n3 adjacent\na 0003 {literal}\na\nb\n3\\n\nf\"{d[\"x\"]}\"\n", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("print(f\"{d[\"x\"]}}\")")]
    [InlineData("print(f\"{d[\"x\"]\")")]
    [InlineData("print(f\"{d[\"x\"]} extra)")]
    public void MalformedFieldsAreRejected(string source)
        => Assert.False(new LythonEngine().Compile(source).IsValid);
}
