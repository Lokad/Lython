using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TypingConstructorCallbackTests
{
    [Theory]
    [InlineData("TypeVar", "covariant")]
    [InlineData("TypeVar", "contravariant")]
    [InlineData("TypeVar", "infer_variance")]
    [InlineData("ParamSpec", "covariant")]
    [InlineData("ParamSpec", "contravariant")]
    public async Task VarianceTruthAwaitsHostWorkAndReleasesItsOwner(string type, string flag)
    {
        var source = "from typing import " + type + "\n" + """
            class Flag:
                def __bool__(self):
                    with open('/flag') as source: return source.read()=='yes'
            """ + "\nT=" + type + "('T'," + flag + "=Flag())\nprint(T.__" + flag + "__)\n";
        var root = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, root))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = root.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/flag", "yes");
            using (var sync = script.Run(immediate))
            {
                Assert.True(sync.Success, sync.Failure?.Message);
                Assert.Equal("True\n", sync.StandardOutput);
            }
            Assert.Equal(baseline, root.ReservedBytes);
            var delayed = new DelayedLythonHost(); delayed.SeedFile("/flag", "yes");
            using (var result = await script.RunAsync(delayed))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("True\n", result.StandardOutput);
                Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, root.ReservedBytes);
        }
        Assert.Equal(0, root.ReservedBytes);
    }
}
