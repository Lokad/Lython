using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FrameIntegerLifetimeTests
{
    private const string Source = """
        def compute():
            total=0
            snapshots=[]
            for value in range(4):
                total+=value
                if value==2:
                    with open('input.txt') as source:
                        total+=int(source.read())
                snapshots.append(total)
            return total,snapshots
        return compute()
        """;

    [Fact]
    public async Task SuspendedWorkingValuesAndEscapingSnapshotsKeepTheirOwners()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var immediate = new MockLythonHost(); immediate.SeedFile("/input.txt", "7");
            using (var first = script.Run(immediate))
            {
                AssertResult(first);
                var retained = budget.ReservedBytes;
                Assert.True(retained > baseline);
                var delayed = new DelayedLythonHost(); delayed.SeedFile("/input.txt", "7");
                using (var second = await script.RunAsync(delayed))
                {
                    AssertResult(second);
                    Assert.True(delayed.CompletedAsynchronously > 0);
                    Assert.True(budget.ReservedBytes > retained);
                    AssertResult(first);
                }
                Assert.Equal(retained, budget.ReservedBytes);
                AssertResult(first);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationJoinsIoBeforeReturningWorkingAllowance()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/input.txt", "7");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var started = host.PauseReadUntilCancellation("/input.txt");
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            }
            finally
            {
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedFramedHelpersReturnWorkingAllowance(bool asynchronous)
    {
        const string source = """
            def fold(values):
                total=0
                try:
                    for value in values:total+=value
                finally:pass
                return total
            values=(0,1,2)
            for ordinal in range(10000):result=fold(values)
            return result
            """;
        // Reuse guest input so conservative retention of newly constructed
        // containers does not obscure relinquished invocation working storage.
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new BigInteger(3), result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static void AssertResult(LythonExecutionResult result)
    {
        Assert.True(result.Success, result.Failure?.Message);
        var values = Assert.IsAssignableFrom<IReadOnlyList<object>>(result.ReturnValue);
        Assert.Equal(new BigInteger(13), values[0]);
        Assert.Equal(new object[] { BigInteger.Zero, BigInteger.One, new BigInteger(10), new BigInteger(13) },
            Assert.IsAssignableFrom<IReadOnlyList<object>>(values[1]));
    }
}
