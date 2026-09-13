using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG03: string literals are shared compile-time constants, free by design
// like interned constants (distinct literal payload stays bounded by
// MaxSourceLength). Retaining one literal a thousand times keeps one copy.
public sealed class LiteralSharingScenarioTests
{
    private static string Big() => new string('A', 500);

    [Fact]
    public async Task LiteralsAliasAcrossEvaluations()
    {
        var script = new LythonEngine().Compile(
            "xs = []\n" +
            "i = 0\n" +
            "while i < 3:\n" +
            "    xs.append(\"" + Big() + "\")\n" +
            "    i = i + 1\n" +
            "return [xs[0] is xs[1], xs[1] is xs[2], len(xs[0])]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, new BigInteger(500) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedSharedLiteralsFit()
    {
        var script = new LythonEngine().Compile(
            "xs = []\n" +
            "i = 0\n" +
            "while i < 2000:\n" +
            "    xs.append(\"" + Big() + "\")\n" +
            "    i = i + 1\n" +
            "return len(xs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2097152 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(2000), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(2000), asyncResult.ReturnValue);
    }
}
