using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R04: list extension reserves growth before allocating, streams unknown-length
/// iterables incrementally (keeping partial effects on failure), and snapshots
/// list-backed inputs (including self) instead of observing their live count.
/// </summary>
public sealed class ListExtendScenarioTests
{
    [Fact]
    public async Task ExtendRangeRejectsBeforeLargeCopy()
    {
        var script = new LythonEngine().Compile(
            """
            items = []
            items.extend(range(10000))
            return len(items)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.Contains("memory budget exceeded", sync.Failure?.Message ?? string.Empty, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("memory budget exceeded", asyncResult.Failure?.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtendFailingGeneratorKeepsPartialEffects()
    {
        var script = new LythonEngine().Compile(
            """
            kept = []
            try:
                kept.extend(1 // x for x in [1, 0])
            except ZeroDivisionError:
                pass
            return kept
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(1) }, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(1) }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task ExtendSelfAppendsOriginalElements()
    {
        var script = new LythonEngine().Compile(
            """
            tiny = [1]
            tiny.extend(tiny)
            wide = list(range(10))
            wide.extend(wide)
            return [tiny, len(wide), wide[10], wide[19]]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(new List<object?> { new BigInteger(1), new BigInteger(1) }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new BigInteger(20), values[1]);
        Assert.Equal(new BigInteger(0), values[2]);
        Assert.Equal(new BigInteger(9), values[3]);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncValues = Assert.IsType<List<object?>>(asyncResult.ReturnValue);
        Assert.Equal(new List<object?> { new BigInteger(1), new BigInteger(1) }, Assert.IsType<List<object?>>(asyncValues[0]));
        Assert.Equal(new BigInteger(20), asyncValues[1]);
    }

    [Fact]
    public async Task ExtendAcrossStorageTransition()
    {
        var script = new LythonEngine().Compile(
            """
            five = [0, 1, 2, 3, 4]
            five.extend(range(5, 10))
            eight = list(range(8))
            eight.extend([8])
            partial = list(range(8))
            try:
                partial.extend(x for x in [8, 9, 10] if x < 10 or 1 // 0 == 0)
            except ZeroDivisionError:
                pass
            return [len(five), five[9], len(eight), eight[8], len(partial), partial[8]]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(new BigInteger(10), values[0]);
        Assert.Equal(new BigInteger(9), values[1]);
        Assert.Equal(new BigInteger(9), values[2]);
        Assert.Equal(new BigInteger(8), values[3]);
        Assert.Equal(new BigInteger(10), values[4]);
        Assert.Equal(new BigInteger(8), values[5]);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(values.Count, Assert.IsType<List<object?>>(asyncResult.ReturnValue).Count);
    }

    [Fact]
    public async Task ExtendInfiniteIterableMeetsMemoryBudget()
    {
        var script = new LythonEngine().Compile(
            """
            import itertools
            items = []
            items.extend(itertools.count())
            return len(items)
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
    public async Task InPlaceAddKeepsPartialEffects()
    {
        var script = new LythonEngine().Compile(
            """
            items = []
            try:
                items += (1 // x for x in [1, 0])
            except ZeroDivisionError:
                pass
            return items
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(1) }, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { new BigInteger(1) }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
}
