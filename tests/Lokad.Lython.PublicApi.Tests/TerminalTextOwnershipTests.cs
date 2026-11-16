using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TerminalTextOwnershipTests
{
    private const string Source = """
        original = 'Aβ\tß\n' * 8
        changed = original.replace('Aβ', 'Ω').expandtabs(4).casefold()
        translated = original.translate({ord('A'): None, ord('β'): 'B'})
        joined = '|'.join([changed, b'\x00\xff'.hex(':'), repr(b'\x00\xff'), translated])
        with open('input.txt') as source: source.read()
        return original, changed, joined, '-12'.zfill(6)
        """;

    [Fact]
    public async Task CompletedTextRemainsFundedThroughIoAndReturnedAliases()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var original = string.Concat(Enumerable.Repeat("Aβ\tß\n", 8));
            var changed = string.Concat(Enumerable.Repeat("ω   ss\n", 8));
            var translated = string.Concat(Enumerable.Repeat("B\tß\n", 8));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var host = new DelayedLythonHost();
                host.SeedFile("/input.txt", "ready");
                using (var result = await script.RunAsync(host))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var values = Assert.IsAssignableFrom<IReadOnlyList<object>>(result.ReturnValue);
                    Assert.Equal(original, values[0]);
                    Assert.Equal(changed, values[1]);
                    Assert.Equal(string.Join("|", changed, "00:ff", "b'\\x00\\xff'", translated), values[2]);
                    Assert.Equal("-00012", values[3]);
                    Assert.True(host.CompletedAsynchronously > 0);
                    Assert.Equal(1, host.ClosedFileCount);
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationJoinsIoBeforeReleasingCompletedText()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/input.txt", "ready");
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
                Assert.Equal(1, host.ClosedFileCount);
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
}
