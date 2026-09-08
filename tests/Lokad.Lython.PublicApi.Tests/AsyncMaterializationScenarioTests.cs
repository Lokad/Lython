using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R04: the shared asynchronous drain charges backing-array growth before it can
/// allocate, so unbounded inputs meet the memory budget in every async builtin.
/// </summary>
public sealed class AsyncMaterializationScenarioTests
{
    [Fact]
    public async Task AsyncListRejectsBeforeLargeCopy()
    {
        var script = new LythonEngine().Compile(
            """
            return list(range(10000))
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("memory budget exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncTupleRejectsBeforeLargeCopy()
    {
        var script = new LythonEngine().Compile(
            """
            return tuple(range(10000))
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("memory budget exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncSortedRejectsBeforeLargeCopy()
    {
        var script = new LythonEngine().Compile(
            """
            return sorted(range(10000), reverse=True)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("memory budget exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsyncSortedGeneratorInputOrdersCorrectly()
    {
        var script = new LythonEngine().Compile(
            """
            return sorted(x * x for x in range(10))
            """);
        Assert.True(script.IsValid);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var values = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal(10, values.Count);
        Assert.Equal(new BigInteger(0), values[0]);
        Assert.Equal(new BigInteger(1), values[1]);
        Assert.Equal(new BigInteger(81), values[9]);
    }

    [Fact]
    public async Task AsyncStarmapAppliesFunction()
    {
        var script = new LythonEngine().Compile(
            """
            import itertools
            return list(itertools.starmap(pow, [(2, 10), (3, 3)]))
            """);
        Assert.True(script.IsValid);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(1024), new BigInteger(27) },
            Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}
