using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class Utf8ByteDecodeTests
{
    private const string Text = "abcdefghijé😀\r\nkept";
    private const string Source = """
        with open('input.bin','rb') as source: payload=source.read()
        decoded=DECODE
        payload=None
        with open('gate.txt') as source: marker=source.read()
        return decoded
        """;

    [Theory]
    [InlineData("payload.decode('utf-8-sig')")]
    [InlineData("str(payload,'utf-8-sig')")]
    public async Task DecodedValuesSurviveInputWithdrawalAndRetainedResults(string decode)
    {
        using var script = new LythonEngine().Compile(Source.Replace("DECODE", decode, StringComparison.Ordinal));
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var immediate = new MockLythonHost();
        immediate.SeedFile("/input.bin", "\uFEFF" + Text); immediate.SeedFile("/gate.txt", "go");
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/input.bin", "\uFEFF" + Text); delayed.SeedFile("/gate.txt", "go");
        using (var first = script.Run(immediate, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(first.Success, first.Failure?.Message);
            Assert.Equal(Text, first.ReturnValue);
            Assert.True(budget.ReservedBytes > 0);
            using (var second = await script.RunAsync(delayed, new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(Text, second.ReturnValue);
                Assert.Equal(Text, first.ReturnValue);
            }
            Assert.True(budget.ReservedBytes > 0);
        }
        Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("payload.decode('utf-8-sig')")]
    [InlineData("str(payload,'utf-8-sig')")]
    public async Task PendingUseStaysFundedUntilCancellationAndCleanupFinish(string decode)
    {
        using var script = new LythonEngine().Compile(Source.Replace("DECODE", decode, StringComparison.Ordinal));
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var host = new DelayedLythonHost();
        host.SeedFile("/input.bin", "\uFEFF" + Text); host.SeedFile("/gate.txt", "go");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var started = host.PauseReadUntilCancellation("/gate.txt");
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        Assert.True(budget.ReservedBytes > 0);
        cancellation.Cancel();
        using (var result = await pending)
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var next = new MockLythonHost();
        next.SeedFile("/input.bin", "\uFEFF" + Text); next.SeedFile("/gate.txt", "go");
        using (var result = script.Run(next, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(Text, result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
