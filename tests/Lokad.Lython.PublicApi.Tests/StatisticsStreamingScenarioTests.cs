using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG15: scalar-output statistics must stream instead of materializing the
/// whole input. statistics.mean over a large lazy range used to build two
/// parallel lists before dividing.
/// </summary>
public sealed class StatisticsStreamingScenarioTests
{
    [Fact]
    public async Task MeanStreamsLargeInputs()
    {
        // MG15 probe: mean(range(100000)) built ~800KB of lists under a 64KiB
        // budget. A streamed count and sum fit easily and divide identically.
        var script = new LythonEngine().Compile(
            """
            import statistics
            return statistics.mean(range(100000))
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(49999.5, Assert.IsType<double>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(49999.5, Assert.IsType<double>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task MeanKeepsValueAndErrorContracts()
    {
        // MG15: streaming must preserve the materialized error precedence
        // (arity, then emptiness, then per-element validation) and the
        // whole-integer result accent.
        var script = new LythonEngine().Compile(
            """
            import statistics
            results = []
            results.append(statistics.mean([1, 2, 3, 4]))
            results.append(statistics.mean([4, 4]))
            try:
                statistics.mean([])
            except statistics.StatisticsError:
                results.append("empty")
            try:
                statistics.mean([1, "x"])
            except TypeError:
                results.append("nonreal")
            try:
                statistics.mean()
            except TypeError:
                results.append("arity")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { 2.5, new BigInteger(4), "empty", "nonreal", "arity" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}
