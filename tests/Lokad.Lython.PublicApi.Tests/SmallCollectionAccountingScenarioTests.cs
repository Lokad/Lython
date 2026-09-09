using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG04: small containers pay for their backing storage, so ordinary records
/// cannot accumulate outside the memory budget.
/// </summary>
public sealed class SmallCollectionAccountingScenarioTests
{
    [Fact]
    public async Task NestedSmallListsStayCharged()
    {
        // MG04 probe: 5,000 six-item lists hold 30,000 reference slots in
        // small backing arrays alone, far above a 256 KiB budget.
        var script = new LythonEngine().Compile(
            """
            return [[0, 1, 2, 3, 4, 5] for i in range(5000)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task SmallDictsBuiltUpStayCharged()
    {
        // MG04 probe: thousands of small governed dicts hold uncharged
        // backing arrays when they never promote to map storage.
        var script = new LythonEngine().Compile(
            """
            out = []
            for i in range(5000):
                out.append(dict())
            return len(out)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task FailedSetGrowthLeavesNothingUsable()
    {
        // MG05: a failed set resize must not leave enlarged uncharged capacity
        // behind for later insertions to ride for free.
        var script = new LythonEngine().Compile(
            """
            def fill(s, start, count):
                added = 0
                i = 0
                while i < count:
                    try:
                        s.add(start + i)
                    except MemoryError:
                        return added
                    added = added + 1
                    i = i + 1
                return added
            s = set()
            first = fill(s, 0, 20000)
            second = fill(s, 100000, 500)
            return [first < 20000, second]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new List<object?> { true, new BigInteger(0) }, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { true, new BigInteger(0) }, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task SetCopiesAndUnionsSucceedWhenFunded()
    {
        // MG05: set.copy/union/operator paths route through the governed
        // snapshot constructor; funded copies must still succeed in both modes.
        var script = new LythonEngine().Compile(
            """
            s = set(range(5000))
            t = s.copy()
            u = s | s
            v = s.union(s)
            return [len(t), len(u), len(v)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
        var expected = new List<object?> { new BigInteger(5000), new BigInteger(5000), new BigInteger(5000) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task SetCopiesOverBudgetFail()
    {
        // MG05: the same copies stay bounded; a budget far below one table
        // charge must fail instead of retaining uncharged copies.
        var script = new LythonEngine().Compile(
            """
            s = set(range(5000))
            t = s.copy()
            u = s | s
            v = s.union(s)
            return [len(t), len(u), len(v)]
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
    public async Task SetFilterOperationsSucceedWhenFunded()
    {
        // MG05: set intersection/difference paths rebuild through governed
        // filter scratch; funded operations must still succeed in both modes.
        var script = new LythonEngine().Compile(
            """
            s = set(range(3000))
            t = set(range(1500, 4500))
            a = s & t
            b = s - t
            c = s ^ t
            d = s.intersection(t)
            e = s.difference(t)
            f = s.symmetric_difference(t)
            return [len(a), len(b), len(c), len(d), len(e), len(f)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
        var expected = new List<object?>
        {
            new BigInteger(1500),
            new BigInteger(1500),
            new BigInteger(3000),
            new BigInteger(1500),
            new BigInteger(1500),
            new BigInteger(3000),
        };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task SetFilterOperationsOverBudgetFail()
    {
        // MG05: the same filters stay bounded; a budget far below one table
        // charge must fail instead of retaining uncharged scratch.
        var script = new LythonEngine().Compile(
            """
            s = set(range(3000))
            t = set(range(1500, 4500))
            a = s & t
            b = s - t
            c = s ^ t
            d = s.intersection(t)
            e = s.difference(t)
            f = s.symmetric_difference(t)
            return [len(a), len(b), len(c), len(d), len(e), len(f)]
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

}
