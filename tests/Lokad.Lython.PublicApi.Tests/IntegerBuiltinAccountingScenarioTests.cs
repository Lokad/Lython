using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG08: integer-producing stdlib builtins own heap-backed results like
/// operators and int() do. One factorial fits, while retaining many results
/// must exceed the budget in both modes.
/// </summary>
public class IntegerBuiltinAccountingScenarioTests
{
    protected virtual int Argument => 512;
    protected virtual int Count => 64;
    protected virtual long MemoryLimit => 16384;

    [Fact]
    public async Task ManyRetainedFactorialsStayCharged()
    {
        var script = new LythonEngine().Compile(
            $"""
            import math
            rs = []
            i = 0
            while i < {Count}:
                rs.append(math.factorial({Argument}))
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MemoryLimit };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task OneFactorialFitsTheRetentionBudget()
    {
        var script = new LythonEngine().Compile($"import math\nreturn math.factorial({Argument}).bit_length()\n");
        Assert.True(script.IsValid);
        var factorial = BigInteger.One;
        for (var i = 2; i <= Argument; i++) factorial *= i;
        var expected = new BigInteger(factorial.GetBitLength());
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MemoryLimit };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
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
