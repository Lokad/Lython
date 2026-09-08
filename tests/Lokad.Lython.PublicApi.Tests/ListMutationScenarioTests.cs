using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// R04: list repetition and mutation scratch is governed before allocation.
/// In-place operations on owned storage replace proportional temporary arrays,
/// with self-referential right-hand sides snapshotted first.
/// </summary>
public sealed class ListMutationScenarioTests
{
    [Fact]
    public async Task InsertRepeatReverseBehaviors()
    {
        var script = new LythonEngine().Compile(
            """
            items = [1, 2, 3]
            items.insert(1, "x")
            items.insert(-99, "head")
            items.insert(99, "tail")
            grown = list(range(8))
            grown.insert(8, 99)
            doubled = [1, 2] * 3
            emptied = [1, 2] * 0
            palindrome = list(range(1000))
            palindrome.reverse()
            palindrome.reverse()
            return [items, grown, doubled, emptied, palindrome == list(range(1000))]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(new List<object?> { "head", new BigInteger(1), "x", new BigInteger(2), new BigInteger(3), "tail" }, Assert.IsType<List<object?>>(values[0]));
        Assert.Equal(new BigInteger(99), Assert.IsType<List<object?>>(values[1])[8]);
        Assert.Equal(6, Assert.IsType<List<object?>>(values[2]).Count);
        Assert.Empty(Assert.IsType<List<object?>>(values[3]));
        Assert.True(Assert.IsType<bool>(values[4]));

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(values.Count, Assert.IsType<List<object?>>(asyncResult.ReturnValue).Count);
    }

    [Fact]
    public async Task SliceAssignAndDeleteBehaviors()
    {
        var script = new LythonEngine().Compile(
            """
            grow = list(range(10))
            grow[2:5] = [7, 7, 7, 7]
            shrink = list(range(10))
            shrink[2:5] = [7]
            same = list(range(10))
            same[2:5] = [7, 7, 7]
            alias = [1, 2, 3]
            alias[0:2] = alias
            trimmed = list(range(10))
            del trimmed[2:8:2]
            stepped = list(range(10))
            stepped[::2] = [70, 71, 72, 73, 74]
            return [grow, shrink, same, alias, trimmed, stepped[0], stepped[9]]
            """);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var values = Assert.IsType<List<object?>>(sync.ReturnValue);
        Assert.Equal(new BigInteger(11), Assert.IsType<List<object?>>(values[0]).Count);
        Assert.Equal(new BigInteger(7), Assert.IsType<List<object?>>(values[0])[2]);
        Assert.Equal(new BigInteger(8), Assert.IsType<List<object?>>(values[1]).Count);
        Assert.Equal(new BigInteger(10), Assert.IsType<List<object?>>(values[2]).Count);
        Assert.Equal(new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(3), new BigInteger(3) }, Assert.IsType<List<object?>>(values[3]));
        Assert.Equal(new BigInteger(7), Assert.IsType<List<object?>>(values[4]).Count);
        Assert.Equal(new BigInteger(70), values[5]);
        Assert.Equal(new BigInteger(9), values[6]);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(values.Count, Assert.IsType<List<object?>>(asyncResult.ReturnValue).Count);
    }

    [Fact]
    public async Task RepeatRejectsBeforeLargeScratch()
    {
        var script = new LythonEngine().Compile(
            """
            items = [1, 2]
            items *= 50000
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
}
