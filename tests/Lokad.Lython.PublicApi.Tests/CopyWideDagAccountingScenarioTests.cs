using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG20: wide graphs copy under the same budget discipline as deep ones.
/// Twenty thousand distinct one-element lists fail a 4MiB budget (memo
/// scratch plus copy construction) and round-trip at 16MiB, in both modes.
/// </summary>
public sealed class CopyWideDagAccountingScenarioTests
{
    private const string BuildWideGraph =
        """
        import copy
        v = [[i] for i in range(20000)]
        m = copy.deepcopy(v)
        return 0
        """;

    [Fact]
    public async Task WideGraphStaysCharged()
    {
        var script = new LythonEngine().Compile(BuildWideGraph);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyWideGraphRoundTrips()
    {
        var script = new LythonEngine().Compile(
            """
            import copy
            v = [[i] for i in range(20000)]
            m = copy.deepcopy(v)
            return [len(m), m[19999][0], m is v]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16777216 };
        var expected = new List<object?> { new BigInteger(20000), new BigInteger(19999), false };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}