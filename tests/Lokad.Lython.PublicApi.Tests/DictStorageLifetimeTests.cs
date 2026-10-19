using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class DictStorageLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EmptyDictionaryTurnoverDoesNotRetainDeletedKeyCharges(bool pop, bool asynchronous)
    {
        var remove = pop ? "d.pop(i + 0)" : "del d[i + 0]";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile($$"""
            d = {}
            with open('/ready', 'r') as stream:
                stream.read()
            for i in range(2000):
                d[i] = None
                {{remove}}
                assert len(d) == 0
            return len(d)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
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
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(BigInteger.Zero, result.ReturnValue);
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 65536);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EscapedWrapperViewsKeepTheirStorageFunded(bool counter, bool asynchronous)
    {
        using var result = await RunWrapperViews(counter, asynchronous);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DefaultdictViewBatchesReturnSharedFundingAfterCleanup(bool asynchronous)
        => VerifyWrapperBatches(counter: false, asynchronous);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CounterViewBatchesReturnSharedFundingAfterCleanup(bool asynchronous)
        => VerifyWrapperBatches(counter: true, asynchronous);

    private static async Task VerifyWrapperBatches(bool counter, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1048576);
        var constructor = counter ? "Counter()" : "defaultdict()";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile($$"""
            from collections import Counter, defaultdict
            with open('/ready', 'r') as stream:
                stream.read()
            for batch in range(4):
                d = {{constructor}}
                for i in range(64):
                    d[i] = i
                view = d.values()
                d = None
                assert sum(view) == 2016
            return 4
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) };
        for (var batch = 0; batch < 8; batch++)
        {
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
            using (result)
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(4), result.ReturnValue);
                Assert.InRange(result.PeakExecutionMemoryBytes, 1, 65536);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async Task<LythonExecutionResult> RunWrapperViews(bool counter, bool asynchronous)
    {
        var constructor = counter ? "Counter()" : "defaultdict()";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile($$"""
            from collections import Counter, defaultdict
            held = []
            with open('/ready', 'r') as stream:
                stream.read()
            for batch in range(200):
                d = {{constructor}}
                for i in range(64):
                    d[i] = i
                held.append(d.values())
                d = None
            return 200
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
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
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 1048576);
        return result;
    }
}
