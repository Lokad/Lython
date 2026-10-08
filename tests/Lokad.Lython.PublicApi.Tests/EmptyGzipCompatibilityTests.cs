using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class EmptyGzipCompatibilityTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(9)]
    public async Task EmptyPayloadsProduceCompleteMembers(int level)
    {
        var script = new LythonEngine().Compile(
            "import gzip\nprint(gzip.decompress(gzip.compress(b''," + level + ",mtime=0)))\n" +
            "with gzip.open('empty.gz','wb',compresslevel=" + level + ") as file: pass\n" +
            "with gzip.open('empty.gz','rb') as file: print(file.read())\n");
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        foreach (var result in new[] { script.Run(immediate), await script.RunAsync(delayed) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("b''\nb''\n", result.StandardOutput);
        }
        Assert.True(delayed.CompletedAsynchronously > 0);
    }
}
