using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: constructed decimal Context values own their storage like other
/// constructed values. Ten thousand retained contexts must exceed a 512KiB
/// budget in both modes.
/// </summary>
public sealed class DecimalContextObjectAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedContextsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            cs = []
            i = 0
            while i < 10000:
                cs.append(decimal.Context())
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task ContextBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Context()
            b = decimal.getcontext().copy()
            return [a.prec, b.prec, a is b]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(28), new BigInteger(28), false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
