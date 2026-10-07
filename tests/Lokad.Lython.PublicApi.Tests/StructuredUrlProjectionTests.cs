using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StructuredUrlProjectionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ResultsProjectAsComponentArrays(bool parameters, bool binary)
    {
        var source = "import urllib.parse as p\nreturn p." + (parameters ? "urlparse(" : "urlsplit(") +
            (binary ? "b" : "") + "'http://X/a;b?q#f')\n";
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        string[] expected = parameters ? ["http", "X", "/a", "b", "q", "f"] : ["http", "X", "/a;b", "q", "f"];
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            var values = Assert.IsType<object?[]>(result.ReturnValue);
            Assert.Equal(expected.Length, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                if (binary) Assert.Equal(System.Text.Encoding.ASCII.GetBytes(expected[i]), Assert.IsType<byte[]>(values[i]));
                else Assert.Equal(expected[i], Assert.IsType<string>(values[i]));
            }
        }
    }
}
