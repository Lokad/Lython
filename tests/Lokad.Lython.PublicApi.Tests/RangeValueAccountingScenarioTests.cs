using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: constructed range objects own their storage like other constructed
/// values. Twenty thousand retained ranges must exceed a 512KiB budget in
/// both modes.
/// </summary>
public sealed class RangeValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedRangesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            rs = []
            i = 0
            while i < 10000:
                rs.append(range(i, i + 100))
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
    public async Task RangeBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile("return [list(range(3)), len(list(range(10)))]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new List<object?> { new BigInteger(0), new BigInteger(1), new BigInteger(2) }, new BigInteger(10) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}