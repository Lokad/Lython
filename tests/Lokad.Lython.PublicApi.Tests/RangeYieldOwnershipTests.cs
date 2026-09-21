using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R03 (remainder): range objects carry their reservations so heap-scale
// yields, subscript results, and slice shells own their magnitudes through
// the shared fresh-magnitude rule. Dropped iterations reclaim on sweep while
// retained populations deny. Small yielded boxes stay uncharged (shell
// calibration below pins the current scale for the R16 provenance design).
public sealed class RangeYieldOwnershipTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscardedBigYieldsSucceed()
    {
        const string code = """
            for v in range(1 << 100000, (1 << 100000) + 1000):
                pass
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public async Task DiscardedBigYieldsSucceedAsync()
    {
        const string code = """
            for v in range(1 << 100000, (1 << 100000) + 1000):
                pass
            """;
        var result = await new LythonEngine().RunAsync(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void RetainedBigYieldsDeny()
    {
        const string code = """
            x = list(range(1 << 100000, (1 << 100000) + 1000))
            return len(x)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void RetainedBigSubscriptsDeny()
    {
        const string code = """
            xs = []
            r = range(1 << 100000, (1 << 100000) + 1000)
            for i in range(1000):
                xs.append(r[i])
            return len(xs)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void DiscardedSubscriptsSucceed()
    {
        const string code = """
            for i in range(5000):
                y = range(1 << 100000, (1 << 100000) + 1000)[i % 100]
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void DiscardedReversedSucceed()
    {
        const string code = """
            for v in reversed(range(1 << 100000, (1 << 100000) + 1000)):
                pass
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void DiscardedSlicesSucceed()
    {
        const string code = """
            for i in range(5000):
                s = range(1 << 1000, (1 << 1000) + 100)[10:20]
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void RetainedBigSlicesDeny()
    {
        const string code = """
            xs = []
            for i in range(200):
                xs.append(range(1 << 100000, (1 << 100000) + 100)[10:20])
            return len(xs)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void SubscriptSliceExact()
    {
        const string code = """
            r = range(5, 50, 7)
            return [r[-1], r[2:5][0], r[0], len(r[10:100])]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { new BigInteger(47), new BigInteger(19), new BigInteger(5), new BigInteger(0) },
            result.ReturnValue);
    }

    [Fact]
    public void SmallYieldShellCalibration()
    {
        // 100k small yields hold no payload charges today: only the range
        // shell and drain traffic account. R16 owns the shell policy; this
        // pins the scale it must cover.
        const string code = """
            for v in range(100000):
                pass
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 131072 });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(result.PeakExecutionMemoryBytes < 4096, $"peak was {result.PeakExecutionMemoryBytes}");
    }
}
