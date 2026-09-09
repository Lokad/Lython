using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG08: math.prod owns its running product like the other accumulations do.
/// Two hundred thousand-digit factors must exceed a 64KiB budget in both
/// modes. Each retained product is small; only their accumulation trips.
/// </summary>
public sealed class MathProdAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedProductsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import math
            xs = [10 ** 100] * 200
            rs = []
            i = 0
            while i < 20:
                rs.append(math.prod(xs))
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ProdBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import math
            return [math.prod([2, 3, 4]), math.prod([], start=5)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(24), new BigInteger(5) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}