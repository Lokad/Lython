using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG08: parsed integers above the inline range own their heap magnitude
/// durably. Parsing alone fits easily, but one hundred retained ~12KiB
/// magnitudes must exceed a 1MiB budget in both modes. The shared literal
/// isolates result growth from per-iteration literal costs.
/// </summary>
public sealed class IntegerParseAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedParsesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            xs = []
            s = "9" * 30000
            i = 0
            while i < 100:
                xs.append(int(s))
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task SmallParsesStillProject()
    {
        var script = new LythonEngine().Compile("return [int(\"42\"), int(\"-7\"), int(3.99)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(42), new BigInteger(-7), new BigInteger(3) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}