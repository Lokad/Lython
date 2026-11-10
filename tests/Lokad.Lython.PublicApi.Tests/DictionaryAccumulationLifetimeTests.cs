using System.Collections;
using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DictionaryAccumulationLifetimeTests
{
    [Fact]
    public async Task ReturnedDictionaryAndOldCountRemainFundedUntilDisposal()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            counts={'x':1000}; key='x'; previous=counts[key]
            for i in range(8): counts[key]=counts.get(key,0)+2
            return [counts,previous,counts[key] is counts.get(key)]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                var values = Assert.IsType<List<object?>>(result.ReturnValue);
                var counts = Assert.IsAssignableFrom<IDictionary>(values[0]);
                Assert.Equal(new BigInteger(1016), Assert.IsType<BigInteger>(counts["x"]));
                Assert.Equal(new BigInteger(1000), Assert.IsType<BigInteger>(values[1]));
                Assert.True(Assert.IsType<bool>(values[2]));
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendingAdditionCancelsAndRerunsWithTheSameAllowance(bool asynchronousRerun)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            class Number:
                def __add__(self,increment):
                    with open('/gate.txt') as source:
                        return int(source.read())+increment
            counts={'x':Number()}; key='x'
            counts[key]=counts.get(key,0)+2
            return counts
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/gate.txt", "5");
        using var cancellation = new CancellationTokenSource();
        var entered = host.PauseReadUntilCancellation("/gate.txt");
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        LythonExecutionResult? result = null;
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Contains("execution canceled", result.Failure?.Message ?? "", StringComparison.Ordinal);
        }
        finally
        {
            cancellation.Cancel();
            result ??= await pending.WaitAsync(TimeSpan.FromSeconds(5));
            result.Dispose();
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var immediate = new MockLythonHost();
        immediate.SeedFile("/gate.txt", "5");
        using (var completed = asynchronousRerun ? await script.RunAsync(immediate) : script.Run(immediate))
        {
            Assert.True(completed.Success, completed.Failure?.Message);
            var counts = Assert.IsAssignableFrom<IDictionary>(completed.ReturnValue);
            Assert.Equal(new BigInteger(7), Assert.IsType<BigInteger>(counts["x"]));
            Assert.True(budget.ReservedBytes > baseline);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusedWideAdditionCannotPublishOrBeSuppressed(bool asynchronous, bool finallyReturn)
    {
        var tail = finallyReturn ? "finally:\n return 0" : "except MemoryError:\n print('caught')";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(
            "counts={'x':1 << 524288}\nkey='x'\namount=1\nprint('ready')\ntry:\n counts[key]=counts.get(key,0)+amount\n"
            + tail + "\nprint('after')\nreturn counts[key]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        ILythonMemoryReservation? padding = null;
        var host = new MockLythonHost
        {
            OnStandardOutputWrite = () =>
            {
                if (padding is null) Assert.True(budget.TryReserve(1024 * 1024 - budget.ReservedBytes - 4096, out padding));
            }
        };
        try
        {
            using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.True(result.FailureAttribution?.Requested >= 65536);
            Assert.Equal("ready\n", result.StandardOutput);
            Assert.NotNull(padding);
        }
        finally { padding?.Dispose(); }
        Assert.Equal(baseline, budget.ReservedBytes);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(finallyReturn ? BigInteger.Zero : (BigInteger.One << 524288) + 1, Assert.IsType<BigInteger>(result.ReturnValue));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
