using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

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
        var script = new LythonEngine().Compile($$"""
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
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
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
        var result = await RunWrapperViews(counter, asynchronous, retain: true);
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DroppedDefaultdictViewCyclesKeepABoundedBudget(bool asynchronous)
    {
        var result = await RunWrapperViews(counter: false, asynchronous, retain: false);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(200), result.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DroppedCounterViewCyclesKeepABoundedBudget(bool asynchronous)
    {
        var result = await RunWrapperViews(counter: true, asynchronous, retain: false);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new BigInteger(200), result.ReturnValue);
    }

    private static async Task<LythonExecutionResult> RunWrapperViews(bool counter, bool asynchronous, bool retain)
    {
        var constructor = counter ? "Counter()" : "defaultdict()";
        var store = retain ? "held.append(d.values())" : "view = d.values()";
        var script = new LythonEngine().Compile($$"""
            from collections import Counter, defaultdict
            held = []
            with open('/ready', 'r') as stream:
                stream.read()
            for batch in range(200):
                d = {{constructor}}
                for i in range(64):
                    d[i] = i
                {{store}}
                d = None
                print('collect')
            return 200
            """);
        Assert.True(script.IsValid);
        Action collect = () =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        };
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        LythonExecutionResult result;
        if (asynchronous)
        {
            var host = new DelayedLythonHost { OnStandardOutputWrite = collect };
            host.SeedFile("/ready", "ready");
            result = await script.RunAsync(host, options);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        else
        {
            var host = new MockLythonHost { OnStandardOutputWrite = collect };
            host.SeedFile("/ready", "ready");
            result = script.Run(host, options);
        }
        Assert.InRange(result.PeakExecutionMemoryBytes, 1, 1048576);
        return result;
    }
}
