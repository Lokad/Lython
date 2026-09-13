using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG06: strip-family trimming scanned one governed rune-string per boundary
// character, so padding-heavy inputs committed durable scratch beside the
// result (20000 padded 500-char strips peaked 28.6MB). Trimming now compares
// runes directly over the UTF-8 span and slices once, so only the result
// accumulates.
public sealed class StripScratchAccountingScenarioTests
{
    [Fact]
    public async Task ManyStrippedStringsStayBounded()
    {
        var big = new string('A', 500);
        var script = new LythonEngine().Compile(
            "b = \"  \" + \"" + big + "\" + \"  \"\n" +
            "xs = []\n" +
            "i = 0\n" +
            "while i < 20000:\n" +
            "    xs.append(b.strip())\n" +
            "    i = i + 1\n" +
            "return len(xs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 18874368 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(20000), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(20000), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StripBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            "return [\"  a  \".strip(), \"  a  \".lstrip(), \"  a  \".rstrip(), \"xxaxx\".strip(\"x\"), \"\\u00a0x\\u00a0\".strip(), \"abc\".strip()]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "a", "a  ", "  a", "a", "x", "abc" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
