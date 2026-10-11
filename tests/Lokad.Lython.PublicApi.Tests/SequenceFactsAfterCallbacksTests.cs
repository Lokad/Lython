using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SequenceFactsAfterCallbacksTests
{
    [Theory]
    [InlineData("def push():items.append(3)\nitems=[]\npush()\nprint(items[0])\n")]
    [InlineData("class Target:\n def push(self):items.append(3)\ntarget=Target()\nitems=[]\ntarget.push()\nprint(items[0])\n")]
    [InlineData("class Descriptor:\n def __set__(self,obj,value):items.append(value)\nclass Target:value=Descriptor()\ntarget=Target()\nitems=[]\ntarget.value=3\nprint(items[0])\n")]
    [InlineData("class Target:\n def __setitem__(self,key,value):items.append(value)\ntarget=Target()\nitems=[]\ntarget[0]=3\nprint(items[0])\n")]
    [InlineData("class Target:\n def __setitem__(self,key,value):items.append(value)\ntarget=Target()\nitems=[]\ntarget[:]=3\nprint(items[0])\n")]
    public async Task UserCallbackCanChangePreviouslyKnownSequenceLength(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("3\n", result.StandardOutput);
        }
    }

    [Fact]
    public void KnownScalarBuiltinCallKeepsProvenOutOfRangeDiagnostic()
    {
        var script = new LythonEngine().Compile("items=[]\nlen('abc')\nprint(items[0])\n");
        Assert.False(script.IsValid);
        Assert.Contains(script.Diagnostics, d => d.Code == "LA3117");
    }
}
