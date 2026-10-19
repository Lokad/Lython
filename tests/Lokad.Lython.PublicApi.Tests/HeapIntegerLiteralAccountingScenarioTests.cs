using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Immutable parsed magnitudes and repeated scalar values retain Python behavior
// under capacity funding. Ordinary controls are bounded; Stress keeps 40k appends.
public sealed class HeapIntegerLiteralAccountingScenarioTests
{
    private static string Big() => new string('9', 30);

    [Fact]
    public async Task HeapLiteralsSharedVersusOwned()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "xs = []\n" +
            "i = 0\n" +
            "while i < 4096:\n" +
            "    xs.append(" + Big() + ")\n" +
            "    i = i + 1\n" +
            "return len(xs)\n");
        Assert.True(script.IsValid);
        var expected = new BigInteger(4096);
        // The supplied allowance funds retained capacity and replacement overlap.
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(5242880) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SmallLiteralsAppendAtScale()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "xs = []\n" +
            "i = 0\n" +
            "while i < 4096:\n" +
            "    xs.append(7)\n" +
            "    i = i + 1\n" +
            "return len(xs)\n");
        Assert.True(script.IsValid);
        // The supplied allowance funds retained capacity and replacement overlap.
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(5242880) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(4096), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(4096), asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Stress")]
    public async Task OriginalScaleLiteralAppendRemainsFunded(bool heapLiteral)
    {
        var value = heapLiteral ? Big() : "7";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            $"xs = []\nfor i in range(40000):\n xs.append({value})\nreturn len(xs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16 * 1024 * 1024) };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new BigInteger(40000), result.ReturnValue);
        }
    }

    [Fact]
    public async Task BigLiteralValuesStillProject()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return " + Big() + "\n");
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
