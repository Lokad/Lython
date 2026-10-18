using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinCallbackLifetimeTests
{
    [Fact]
    public async Task OverlappingSortCallbacksKeepPrivateRunsAndRetainedResultFunding()
    {
        const string source = """
import builtins
assert sorted.__self__ is builtins
def key(value):
    with open('audit.txt','a') as output: output.write(tag + str(value))
    return -value
return sorted(items, key=key)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var pausedHost = new DelayedLythonHost();
        pausedHost.SeedFile("/audit.txt", "before");
        var entered = pausedHost.PauseWriteUntilCancellation("/audit.txt");
        using var cancellation = new CancellationTokenSource();
        var firstOptions = new LythonRunOptions { Globals = new Dictionary<string, object?>
            { ["tag"] = "first", ["items"] = new object?[] { new BigInteger(3), new BigInteger(1), new BigInteger(2) } } };
        var pending = script.RunAsync(pausedHost, firstOptions, cancellation.Token);
        try
        {
            var ready = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
            if (ReferenceEquals(ready, pending))
            {
                using var early = await pending;
                Assert.Fail(early.Failure?.Message ?? "Sort finished before controlled callback suspension.");
            }
            Assert.Same(entered, ready);
            Assert.False(pending.IsCompleted);
            var suspended = budget.ReservedBytes;
            Assert.True(suspended > baseline);
            var otherHost = new DelayedLythonHost();
            otherHost.SeedFile("/audit.txt", "before");
            var otherOptions = new LythonRunOptions { Globals = new Dictionary<string, object?>
                { ["tag"] = "other", ["items"] = new object?[] { new BigInteger(6), new BigInteger(4), new BigInteger(5) } } };
            using (var retained = await script.RunAsync(otherHost, otherOptions).WaitAsync(TimeSpan.FromSeconds(10)))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                var expected = new object?[] { new BigInteger(6), new BigInteger(5), new BigInteger(4) };
                Assert.Equal(expected, Assert.IsType<List<object?>>(retained.ReturnValue));
                Assert.True(budget.ReservedBytes > suspended);
                Assert.False(pending.IsCompleted);
                cancellation.Cancel();
                using (var cancelled = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
                {
                    Assert.False(cancelled.Success);
                    Assert.Equal(LythonFailureReason.Cancelled, cancelled.FailureAttribution?.Reason);
                }
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal(expected, Assert.IsType<List<object?>>(retained.ReturnValue));
                Assert.Equal("before", pausedHost.ReadText("/audit.txt"));
                Assert.Equal("beforeother6other4other5", otherHost.ReadText("/audit.txt"));
                Assert.True(pausedHost.CompletedAsynchronously > 0);
                Assert.True(otherHost.CompletedAsynchronously > 0);
            }
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SortCallbackRetainsArgumentsThroughHostIoAndCleanup(bool cancel, bool expanded)
    {
        var source = "def key(value):\n    with open('audit.txt','a') as output: output.write(str(value))\n    return -value\n"
            + "target=sorted\nordered=" + (expanded ? "target(*[[3,1,2]],**{'key':key})" : "target([3,1,2],key=key)")
            + "\nprint(ordered)\nreturn ordered";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/audit.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/audit.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                await entered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            if (cancel)
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Empty(result.StandardOutput);
                Assert.Equal("before", host.ReadText("/audit.txt"));
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("[3, 2, 1]\n", result.StandardOutput);
                Assert.Equal(new object?[] { new BigInteger(3), new BigInteger(2), new BigInteger(1) },
                    Assert.IsType<List<object?>>(result.ReturnValue));
                Assert.Equal("before312", host.ReadText("/audit.txt"));
            }
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
