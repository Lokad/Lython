using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MappingSliceKeyCompatibilityTests
{
    [Theory]
    [InlineData("dict()")]
    [InlineData("Counter()")]
    [InlineData("defaultdict()")]
    [InlineData("ChainMap()")]
    public async Task ColonLookupEqualReplacementAndDeletionMatchPython(string factory)
    {
        var source = "from collections import Counter,defaultdict,ChainMap\nmapping=" + factory + "\n" + """
            mapping[3:5:2]=[7]
            original=next(iter(mapping))
            mapping[3:5:2]=[8]
            print(next(iter(mapping)) is original,mapping[3:5:2])
            del mapping[3:5:2]
            print(len(mapping))
            """;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True [8]\n0\n", result.StandardOutput);
        }
    }
}
