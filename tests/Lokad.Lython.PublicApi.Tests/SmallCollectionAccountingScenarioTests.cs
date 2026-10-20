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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            return [[0, 1, 2, 3, 4, 5] for i in range(5000)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            out = []
            for i in range(5000):
                out.append(dict())
            return len(out)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task SetGrowthChargesStorage()
    {
        // Set growth and retained entries stay conservatively charged, so
        // successful insertion cannot retain substantial storage outside the
        // execution allowance. The floor detects loss of that storage funding;
        // deterministic before-mutation denial is covered by native controls.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            s = set()
            for i in range(3000):
                s.add(i)
            return len(s)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var expected = new BigInteger(3000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        Assert.True(sync.PeakExecutionMemoryBytes >= 200000, $"peak {sync.PeakExecutionMemoryBytes}");
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.True(asyncResult.PeakExecutionMemoryBytes >= 200000, $"peak {asyncResult.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public async Task SetCopiesAndUnionsSucceedWhenFunded()
    {
        // MG05: set.copy/union/operator paths route through the governed
        // snapshot constructor; funded copies must still succeed in both modes.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            s = set(range(5000))
            t = s.copy()
            u = s | s
            v = s.union(s)
            return [len(t), len(u), len(v)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4194304) };
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            s = set(range(5000))
            t = s.copy()
            u = s | s
            v = s.union(s)
            return [len(t), len(u), len(v)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
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
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(4194304) };
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
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
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
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task BytesSlicesSucceedWhenFunded()
    {
        // Normalized slices admit their final backing directly.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            data = bytes(10240)
            a = data[::2]
            b = data[100:9000:3]
            c = data[::-1]
            return [len(a), len(b), len(c)]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var expected = new List<object?> { new BigInteger(5120), new BigInteger(2967), new BigInteger(10240) };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task BytesSlicesOverBudgetFail()
    {
        // Source plus four retained copies exceed 32 KiB even without scratch
        // or duplicate charges. Three copies now fit after consolidation.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            data = bytes(10240)
            a = data[::2]
            b = data[100:9000:3]
            c = data[::-1]
            d = data[:]
            return [len(a), len(b), len(c), len(d)]
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(32768);
        var options = new LythonRunOptions { MemoryBudget = budget };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("a, b", "value")]
    [InlineData("a, *b", "memory")]
    public async Task UnpackingReadRequirementsPreserveArityAndBudgetErrors(string target, string expected)
    {
        // Fixed arity needs only one excess value. Starred unpacking must
        // retain the remainder and still meet its allocation budget. Even
        // reference storage alone for 4,096 values exceeds this allowance.
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            $"""
            try:
                try:
                    {target} = range(4096)
                except ValueError:
                    return "value"
            except MemoryError:
                return "memory"
            return "neither"
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            if (expected == "memory")
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.ReturnValue);
            }
        }
    }

    [Fact]
    public async Task UnpackingSucceedsWhenFunded()
    {
        // MG05: funded fixed-arity and starred unpacking must still succeed in
        // both modes.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            a, *b = range(5)
            c, d = [10, 20]
            return [a, len(b), c, d]
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var expected = new List<object?> { new BigInteger(0), new BigInteger(4), new BigInteger(10), new BigInteger(20) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

}
