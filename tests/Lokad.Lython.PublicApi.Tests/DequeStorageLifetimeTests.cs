using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DequeStorageLifetimeTests
{
    private const string Yield = """
        with open('/ready', 'r') as stream:
            stream.read()

        """;

    private static async Task<LythonExecutionResult> Run(string source, bool asynchronous, long budget)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(budget) };
        LythonExecutionResult result;
        if (asynchronous)
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/ready", "ready");
            result = await script.RunAsync(host, options);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        else
        {
            var host = new MockLythonHost();
            host.SeedFile("/ready", "ready");
            result = script.Run(host, options);
        }
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, budget);
        return result;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedAliasesConsumeLiveNodeStorage(bool asynchronous)
    {
        using var result = await Run(Yield + """
            from collections import deque
            d = deque()
            value = 7
            for _ in range(10000):
                d.append(value)
            return len(d)
            """, asynchronous, 1048576);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedLargeValueSurvivesSourceDropAndYield(bool asynchronous)
    {
        using var result = await Run("""
            from collections import deque
            d = deque([1 << 4000], maxlen=1)
            held = d[0]
            method = held.bit_length
            d.clear()
            del d
            """ + "\n" + Yield + """
            d = deque(maxlen=1)
            for _ in range(8):
                d.append(1 << 4000)
            return method()
            """, asynchronous, 262144);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(4001), result.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EscapedPayloadsStayFundedAfterBoundedEviction(bool asynchronous)
    {
        using var result = await Run(Yield + """
            from collections import deque
            d = deque(maxlen=1)
            held = []
            for _ in range(2000):
                d.append(1 << 4000)
                held.append(d[0])
            return len(held)
            """, asynchronous, 262144);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CycleAndMixedBoundedTurnoverPreserveIdentity(bool asynchronous)
    {
        using var result = await Run(Yield + """
            from collections import deque
            d = deque(maxlen=2)
            d.append(d)
            d.appendleft(d)
            assert d.pop() is d
            assert d[0] is d
            for i in range(10000):
                d.append(i % 2 == 0)
                d.appendleft(None)
                d.rotate(1)
                d[0] = d
            return d[0] is d
            """, asynchronous, 262144);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(Assert.IsType<bool>(result.ReturnValue));
    }
}
