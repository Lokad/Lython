using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG12: combinatoric input pools stay owned by their iterators, so lazy inputs
/// cannot materialize outside the memory budget before producing anything.
/// </summary>
public sealed class ItertoolsPoolAccountingScenarioTests
{
    [Fact]
    public async Task ProductPoolStaysCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import itertools
            return itertools.product(range(100000))
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
    public async Task CombinationsPoolStaysCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import itertools
            return itertools.combinations(range(100000), 2)
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
    public async Task RepeatValueStaysConstantWithoutPooling()
    {
        // MG12: itertools.repeat holds one shared value plus a counter, so an
        // absurd repeat count must neither allocate nor fail under a tiny
        // budget, and zero counts exhaust immediately.
        var script = new LythonEngine().Compile(
            """
            import itertools
            r = itertools.repeat("abc", 10 ** 18)
            a = next(r)
            b = next(r)
            c = 0
            for x in itertools.repeat("z", 0):
                c = c + 1
            return [a, b, c]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var expected = new List<object?> { "abc", "abc", new BigInteger(0) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task ExhaustedPoolsStayCharged()
    {
        // MG12: pools stay owned by their iterators even when the iterator
        // is exhausted without ever yielding. An empty pool exhausts the
        // product immediately, so no result tuple exists to hide behind:
        // only retained pool charges can fail this script.
        var script = new LythonEngine().Compile(
            """
            import itertools
            holders = []
            for i in range(300):
                holders.append(itertools.product(range(200), []))
            return len(holders)
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
    public async Task ExhaustedIteratorStaysUsable()
    {
        // MG12: a retained exhausted iterator keeps yielding nothing instead
        // of failing or losing its pools, and later iterators still work.
        var script = new LythonEngine().Compile(
            """
            import itertools
            p = itertools.product([1, 2], [3])
            first = list(p)
            second = list(p)
            q = list(itertools.product([5], [6]))
            return [len(first), len(second), len(q)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var expected = new List<object?> { new BigInteger(2), new BigInteger(0), new BigInteger(1) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

}
