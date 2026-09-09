using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: DecimalTuple values own their digit-tuple backing plus one wrapper slot.
/// Five thousand retained tuples must exceed a 512KiB budget in both modes.
/// </summary>
public sealed class DecimalTupleAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedTuplesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            a = decimal.Decimal("1.23456789")
            ds = []
            i = 0
            while i < 5000:
                ds.append(a.as_tuple())
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
    public async Task TupleBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import decimal
            t = decimal.Decimal("1.5").as_tuple()
            u = decimal.DecimalTuple(0, (1, 5), -1)
            checks = [t[0] == 0, t[2] == -1, len(t) == 3]
            checks.append(len(t[1]) == 2)
            checks.append(u[2] == -1)
            return checks
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
