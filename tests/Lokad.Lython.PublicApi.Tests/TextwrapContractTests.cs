using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TextwrapContractTests
{
    [Theory]
    [InlineData("textwrap.dedent()")]
    [InlineData("textwrap.indent('x')")]
    [InlineData("textwrap.indent('x','>',unexpected=True)")]
    public void KnownCallsRejectInvalidBinding(string call)
    {
        var script = new LythonEngine().Compile("import textwrap\n" + call);
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == "LA3151");
    }

    [Theory]
    [InlineData("wrap")]
    [InlineData("fill")]
    [InlineData("shorten")]
    [InlineData("TextWrapper")]
    public async Task UninventoriedMembersFailStaticallyAndThroughDynamicLookup(string name)
    {
        var direct = new LythonEngine().Compile("import textwrap\ntextwrap." + name);
        Assert.False(direct.IsValid);
        Assert.Contains(direct.Diagnostics, diagnostic => diagnostic.Code == "LA3113");
        var dynamic = new LythonEngine().Compile("import textwrap\ngetattr(textwrap,'" + name + "')");
        Assert.True(dynamic.IsValid, string.Join("; ", dynamic.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { dynamic.Run(new MockLythonHost()), await dynamic.RunAsync(new MockLythonHost()) })
        {
            Assert.False(result.Success);
            Assert.Equal("AttributeError", result.Failure?.ExceptionType);
        }
    }
}
