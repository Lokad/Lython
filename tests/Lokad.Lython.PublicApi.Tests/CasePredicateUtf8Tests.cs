using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CasePredicateUtf8Tests
{
    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("123 😀", false, false, false)]
    [InlineData("abc123", true, false, false)]
    [InlineData("ABC123", false, true, false)]
    [InlineData("Abc 123", false, false, true)]
    [InlineData("λ123", true, false, false)]
    [InlineData("Λ123", false, true, true)]
    [InlineData("Λλ", false, false, true)]
    [InlineData("Λλ Λλ", false, false, true)]
    [InlineData("λΛ", false, false, false)]
    [InlineData("ǅ", false, false, true)]
    [InlineData("ǅǆ", false, false, true)]
    [InlineData("Ǆ", false, true, true)]
    [InlineData("ǆ", true, false, false)]
    [InlineData("ᵃ", true, false, false)]
    [InlineData("ᴬ", true, false, false)]
    [InlineData("ⓐ", true, false, false)]
    [InlineData("Ⓐ", false, true, true)]
    [InlineData("ⅰ", true, false, false)]
    [InlineData("Ⅰ", false, true, true)]
    [InlineData("𝔞", true, false, false)]
    [InlineData("𝔄", false, true, true)]
    [InlineData("Áb", false, false, false)]
    [InlineData("ÁB", false, true, true)]
    [InlineData("á", true, false, false)]
    [InlineData("ß", true, false, false)]
    [InlineData("ẞ", false, true, true)]
    [InlineData("İ", false, true, true)]
    [InlineData("i̇", true, false, false)]
    [InlineData("A_B", false, true, true)]
    [InlineData("A1B", false, true, true)]
    [InlineData("a\u0000B", false, false, false)]
    [InlineData(" ͅ", true, false, false)]
    public async Task CasePredicatesFollowPython(string value, bool lower, bool upper, bool title)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return [value.islower(),value.isupper(),value.istitle()]");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["value"] = value } };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new List<object?> { lower, upper, title }, result.ReturnValue);
        }
    }
}
