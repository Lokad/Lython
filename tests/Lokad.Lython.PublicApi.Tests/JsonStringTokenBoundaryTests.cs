using Lokad.Lython.Tests.Harness;
using System.Text;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class JsonStringTokenBoundaryTests
{
    [Theory]
    [InlineData(1021, false)]
    [InlineData(1021, true)]
    [InlineData(1023, false)]
    [InlineData(1023, true)]
    [InlineData(1024, false)]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    [InlineData(1025, true)]
    public async Task RawStringWindowsPreserveEscapesControlsAndScalarEnd(int prefixLength, bool strict)
    {
        var prefix = new string('a', prefixLength);
        var token = "\"" + prefix + (strict ? "\\n\\\"\\\\\\u00e9" : "\n\\\"") + "😀" + prefix + "\"";
        var expected = prefix + (strict ? "\n\"\\é" : "\n\"") + "😀" + prefix;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?>
        {
            ["payload"] = token + " ignored",
            ["end"] = token.EnumerateRunes().Count()
        } };
        using var script = new LythonEngine().Compile("import json\nvalue,position=json.JSONDecoder(strict=" +
            (strict ? "True" : "False") + ").raw_decode(payload)\nassert position==end\nreturn value");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
        }
    }
}
