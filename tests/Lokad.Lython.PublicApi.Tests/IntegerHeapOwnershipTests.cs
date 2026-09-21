using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// R11: heap magnitudes produced by ordinary arithmetic (and literals) share
// the fresh-magnitude ownership rule: each box commits its payload and owns
// a pool-tracked coupon released exactly once on collection, so dropped
// values reclaim on sweep. Coupons stay conservative under shared limb
// arrays (charged while shared, exact once fully collected); alias dedup
// keeps a shared box from double-committing. Small magnitudes stay free.
public sealed class IntegerHeapOwnershipTests
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscardedReassignSucceeds()
    {
        const string code = """
            x = 0
            for i in range(20000):
                x = 1 << 10000
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public async Task DiscardedReassignSucceedsAsync()
    {
        const string code = """
            x = 0
            for i in range(20000):
                x = 1 << 10000
            """;
        var result = await new LythonEngine().RunAsync(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void RetainedListDenies()
    {
        const string code = """
            xs = []
            for i in range(20000):
                xs.append(1 << 10000)
            return len(xs)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        AssertError(result, "MemoryError", "execution memory budget exceeded");
    }

    [Fact]
    public void ArithmeticTemporariesSucceed()
    {
        const string code = """
            for i in range(20000):
                z = (1 << 10000) + (1 << 10000) - (1 << 10000)
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void AliasDropReclaims()
    {
        const string code = """
            for i in range(20000):
                x = 1 << 10000
                y = x
                x = 1
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 3145728 });
        Assert.True(result.Success, result.Failure?.Message);
    }

    [Fact]
    public void SmallIntsStayUntracked()
    {
        const string code = """
            x = 0
            for i in range(20000):
                x = 1 << 60
            """;
        var result = new LythonEngine().Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 8192 });
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(result.PeakExecutionMemoryBytes < 4096, $"peak was {result.PeakExecutionMemoryBytes}");
    }

    [Fact]
    public void AliasIdentityExact()
    {
        const string code = """
            x = 1 << 10000
            y = x
            same = x is y
            x = 1
            return [same, y == (1 << 10000), x]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, true, new BigInteger(1) },
            result.ReturnValue);
    }

    [Fact]
    public void RepeatedBoxingExact()
    {
        const string code = """
            a = 10 ** 100
            b = 10 ** 100
            return [a == b, a is b, a + 1 == b + 1, -a == 0 - a]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?> { true, false, true, true },
            result.ReturnValue);
    }

    [Fact]
    public void MixedArithmeticExact()
    {
        const string code = """
            return [(1 << 100) + (1 << 100), (1 << 200) * 3 - 1, (1 << 10000) >> 9999, abs(0 - (1 << 90))]
            """;
        var result = new LythonEngine().Run(code, new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            new List<object?>
            {
                (BigInteger.One << 100) + (BigInteger.One << 100),
                (BigInteger.One << 200) * 3 - 1,
                new BigInteger(2),
                BigInteger.One << 90,
            },
            result.ReturnValue);
    }
}
