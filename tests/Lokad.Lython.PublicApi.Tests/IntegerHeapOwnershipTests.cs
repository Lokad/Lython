using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class IntegerHeapOwnershipTests : LifetimeScenario
{
    private static void AssertError(LythonExecutionResult result, string type, string messagePart)
    {
        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(type, result.Failure!.ExceptionType);
        Assert.Contains(messagePart, result.Failure!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public Task ReassignmentBatchesPreserveValues()
        => AssertBatchCompletes("x = 0\nfor i in range(8):\n    x = 1 << 10000\nreturn x.bit_length() == 10001\n", true, 3145728, asynchronous: false);

    [Fact]
    public Task AsyncReassignmentBatchesPreserveValues()
        => AssertBatchCompletes("x = 0\nfor i in range(8):\n    x = 1 << 10000\nreturn x.bit_length() == 10001\n", true, 3145728, asynchronous: true);

    [Fact]
    public void RetainedListDenies()
    {
        const string code = """
            xs = []
            for i in range(20000):
                xs.append(1 << 10000)
            return len(xs)
            """;
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(
            code,
            new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(3145728) });
        AssertError(result, "MemoryError", "shared memory budget exceeded");
    }

    [Fact]
    public Task ArithmeticBatchesPreserveValues()
        => AssertBatchCompletes("for i in range(8):\n    z = (1 << 10000) + (1 << 10000) - (1 << 10000)\nreturn z.bit_length() == 10001\n", true, 3145728, asynchronous: false);

    [Fact]
    public Task AliasBatchesKeepTheirValuesAfterWithdrawal()
        => AssertBatchCompletes("for i in range(8):\n    x = 1 << 10000\n    y = x\n    x = 1\nreturn y.bit_length() == 10001 and x == 1\n", true, 3145728, asynchronous: false);

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
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(code, new MockLythonHost());
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
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(code, new MockLythonHost());
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
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run(code, new MockLythonHost());
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
