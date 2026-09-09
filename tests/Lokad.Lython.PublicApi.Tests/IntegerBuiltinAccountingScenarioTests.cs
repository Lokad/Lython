using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG08: integer-producing stdlib builtins own heap-backed results like
/// operators and int() do. One hundred retained ~32KiB factorials must exceed
/// a 2MiB budget in both modes.
/// </summary>
public sealed class IntegerBuiltinAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedFactorialsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import math
            rs = []
            i = 0
            while i < 100:
                rs.append(math.factorial(20000))
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 2097152 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task IntegerBuiltinBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import math
            return [math.factorial(5), math.gcd(12, 18), round(3.5), math.trunc(9.99)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(120), new BigInteger(6), new BigInteger(4), new BigInteger(9) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}