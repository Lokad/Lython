using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HtmlContractTests
{
    [Theory]
    [InlineData("html.escape()")]
    [InlineData("html.escape('x',False,True)")]
    [InlineData("html.unescape()")]
    [InlineData("html.unescape('x','y')")]
    [InlineData("html.unescape('x',unexpected=True)")]
    public void KnownCallsRejectInvalidBinding(string call)
    {
        var script = new LythonEngine().Compile("import html\n" + call);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA3151");
    }

    [Fact]
    public async Task ParserAndEntityModuleImportsRemainExplicitlyUnsupported()
    {
        foreach (var name in new[] { "html.parser", "html.entities" })
        {
            var script = new LythonEngine().Compile("import importlib\nimportlib.import_module('" + name + "')");
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
            {
                Assert.False(result.Success);
                Assert.Equal("ModuleNotFoundError", result.Failure?.ExceptionType);
            }
        }
    }

    [Fact]
    public async Task NamedDecodingDoesNotUseTheGuestCollectionLimit()
    {
        var script = new LythonEngine().Compile("import html\nprint(html.unescape('&amp;&NotEqualTilde;&Tab;é😀'))");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions { MaxCollectionSize = 1 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("&≂̸\té😀\n", result.StandardOutput);
        }
    }
}
