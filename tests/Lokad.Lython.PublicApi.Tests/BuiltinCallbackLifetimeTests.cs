using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BuiltinCallbackLifetimeTests
{
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
