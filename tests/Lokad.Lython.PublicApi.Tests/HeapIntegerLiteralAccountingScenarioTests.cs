using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG08: heap-sized integer literals share their parsed magnitude per
// compilation like interned string constants (source-bounded and immutable),
// so loop-carried big literals complete on every path; computed heap results
// keep their own magnitude ownership and inline-range values stay free.
// the lowered path rebuilds them per evaluation and must charge each copy.
public sealed class HeapIntegerLiteralAccountingScenarioTests
{
    private static string Big() => new string('9', 30);

    [Fact]
    public async Task HeapLiteralsSharedVersusOwned()
    {
        var script = new LythonEngine().Compile(
            "xs = []\n" +
            "i = 0\n" +
            "while i < 40000:\n" +
            "    xs.append(" + Big() + ")\n" +
            "    i = i + 1\n" +
            "return len(xs)\n");
        Assert.True(script.IsValid);
        var expected = new BigInteger(40000);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1677722 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SmallLiteralsStayFree()
    {
        var script = new LythonEngine().Compile(
            "xs = []\n" +
            "i = 0\n" +
            "while i < 40000:\n" +
            "    xs.append(7)\n" +
            "    i = i + 1\n" +
            "return len(xs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1677722 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(40000), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(40000), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BigLiteralValuesStillProject()
    {
        var script = new LythonEngine().Compile("return " + Big() + "\n");
        Assert.True(script.IsValid);
        var expected = BigInteger.Parse(Big(), System.Globalization.CultureInfo.InvariantCulture);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
