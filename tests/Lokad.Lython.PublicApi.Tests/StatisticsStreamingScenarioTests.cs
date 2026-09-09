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
    [Fact]
    public async Task VarianceDrainStaysCharged()
    {
        // MG15: multi-pass statistics share the governed numeric drain. The
        // 100,000 doubles used to materialize free; now their backing alone
        // exceeds the budget before any pass runs.
        var script = new LythonEngine().Compile(
            """
            import statistics
            return statistics.pvariance(range(100000))
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
    public async Task VarianceKeepsValueAndErrorContracts()
    {
        // MG15: governing the drain must preserve values and the empty /
        // non-real error contracts of the sorting paths.
        var script = new LythonEngine().Compile(
            """
            import statistics
            results = []
            results.append(statistics.pvariance([2, 4, 4, 4, 5, 5, 7, 9]))
            try:
                statistics.pvariance([])
            except statistics.StatisticsError:
                results.append("empty")
            try:
                statistics.pvariance([1, "x"])
            except TypeError:
                results.append("nonreal")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { 4.0, "empty", "nonreal" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task MedianDrainStaysCharged()
    {
        // MG15: median keeps the objects list and the converted doubles list
        // alive together. Both used to materialize free; now their combined
        // backing alone exceeds the budget.
        var script = new LythonEngine().Compile(
            """
            import statistics
            return statistics.median(range(100001))
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
    public async Task MedianKeepsValueAndErrorContracts()
    {
        // MG15: governing the objects drain and the converted copy must
        // preserve median values and the empty / non-real error contracts.
        var script = new LythonEngine().Compile(
            """
            import statistics
            results = []
            results.append(statistics.median([1, 4, 2, 3]))
            results.append(statistics.median_low([1, 2, 3, 4]))
            results.append(statistics.median_high([1, 2, 3, 4]))
            try:
                statistics.median([])
            except statistics.StatisticsError:
                results.append("empty")
            try:
                statistics.median_low([1, "x"])
            except TypeError:
                results.append("nonreal")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { 2.5, new BigInteger(2), new BigInteger(3), "empty", "nonreal" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task ModeFrequencyMapStaysCharged()
    {
        // MG15: mode builds a frequency table plus order and result lists over
        // the drained values. All three used to materialize free; now their
        // combined backing alone exceeds the budget.
        var script = new LythonEngine().Compile(
            """
            import statistics
            return statistics.mode(range(100000))
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
    public async Task ModeKeepsValueAndErrorContracts()
    {
        // MG15: governing the frequency map must preserve mode values over
        // string keys as well as the empty / unhashable error contracts.
        var script = new LythonEngine().Compile(
            """
            import statistics
            results = []
            results.append(statistics.mode([3, 1, 3, 2, 2, 3]))
            results.append(statistics.mode("abac"))
            results.append(len(statistics.multimode([1, 2, 1, 2, 3])))
            try:
                statistics.mode([])
            except statistics.StatisticsError:
                results.append("empty")
            try:
                statistics.mode([[1], [1]])
            except TypeError:
                results.append("unhashable")
            return results
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { new BigInteger(3), "a", new BigInteger(2), "empty", "unhashable" };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}
