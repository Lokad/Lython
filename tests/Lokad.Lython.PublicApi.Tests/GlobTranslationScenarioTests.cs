using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GlobTranslationScenarioTests
{
    // Frozen CPython 3.13 match semantics with explicit contained separators.
    [Theory]
    [InlineData("*", "", false, true, "/", false)]
    [InlineData("*", "a", false, false, "/", true)]
    [InlineData("*", ".a", false, false, "/", false)]
    [InlineData("*", ".a", false, true, "/", true)]
    [InlineData("a/**/b", "a/b", true, false, "/", true)]
    [InlineData("a/**/b", "a/x/y/b", true, false, "/", true)]
    [InlineData("a/**/b", "a/.x/b", true, false, "/", false)]
    [InlineData("a/**/b", "a/.x/b", true, true, "/", true)]
    [InlineData("a/**/**/b", "a/x/b", true, false, "/", true)]
    [InlineData("**/*.py", "a.py", true, false, "/", true)]
    [InlineData("**/*.py", "x/.a.py", true, false, "/", false)]
    [InlineData("**/*.py", "x/.a.py", true, true, "/", true)]
    [InlineData("a😀b", "a😀b", false, false, "😀", true)]
    [InlineData("a/b", "a\\b", false, false, "/\\", true)]
    [InlineData("[😀-🙏]", "🙏", false, false, "/", true)]
    [InlineData("x", "x\n", false, false, "/", false)]
    public async Task TranslationPreservesPathSegments(string pattern, string name, bool recursive,
        bool hidden, string separators, bool expected)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import glob,re
            return re.match(glob.translate(pattern,recursive=recursive,include_hidden=hidden,seps=separators),name) is not None
            """);
        Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
        var globals = new Dictionary<string, object?>
        {
            ["pattern"] = pattern, ["name"] = name, ["recursive"] = recursive,
            ["hidden"] = hidden, ["separators"] = separators
        };
        using var sync = script.Run(new MockLythonHost(), globals);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        using var asynchronous = await script.RunAsync(new MockLythonHost(), globals);
        Assert.True(asynchronous.Success, asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.ReturnValue);
    }
}
