using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ModernTypingScenarioTests
{
    [Theory]
    [InlineData("type A=compute()", "A.__value__", "True True")]
    [InlineData("def f[T:compute()]():pass", "f.__type_params__[0].__bound__", "True True")]
    [InlineData("def f[T=compute()]():pass", "f.__type_params__[0].__default__", "True True")]
    public async Task LazyValuesAwaitHostAndCacheSuccess(string declaration, string access, string expected)
    {
        var script = Compile("events=[]\ndef compute():\n    with open('/data.txt') as f:events.append(f.read())\n    return int\n" + declaration + "\nprint(events)\nprint(" + access + " is int," + access + " is int)\nprint(events)\n");
        var host = new MockLythonHost(); host.SeedFile("/data.txt", "!");
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("[]\n" + expected + "\n['!']\n", sync.StandardOutput);
        var delayed = new DelayedLythonHost(); delayed.SeedFile("/data.txt", "!");
        var result = await script.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("def f[T](x:compute()):pass")]
    [InlineData("class C[T]:\n    x:compute()")]
    public async Task EagerAnnotationsAwaitHost(string declaration)
    {
        var script = Compile("events=[]\ndef compute():\n    with open('/data.txt') as f:events.append(f.read())\n    return int\n" + declaration + "\nprint(events)\n");
        var host = new DelayedLythonHost(); host.SeedFile("/data.txt", "!");
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['!']\n", result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task RetainedAliasesAndAnnotationScopesRemainCharged()
    {
        var script = Compile("kept=[]\nfor i in range(10000):\n    type A[T]=list[T]\n    kept.append(A)\nprint('unreachable')\n");
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success); Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal("", result.StandardOutput);
        }
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        return script;
    }
}
