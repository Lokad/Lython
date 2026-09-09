using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG08: integer power and shift results own their estimated payload
/// durably. Each operation alone fits easily, but one hundred retained
/// ~12KiB results must exceed a 1MiB budget in both modes.
/// </summary>
public sealed class IntegerPayloadAccountingScenarioTests
{
    [Fact]
    public async Task ManyRetainedShiftsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            rs = []
            i = 0
            while i < 100:
                rs.append(1 << 100000)
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
    public async Task ManyRetainedPowersStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            rs = []
            i = 0
            while i < 100:
                rs.append(2 ** 100000)
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
    public async Task SmallPowersAndShiftsStillProject()
    {
        var script = new LythonEngine().Compile("return [(1 << 10), (2 ** 10), (3 << 0)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1024), new BigInteger(1024), new BigInteger(3) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}