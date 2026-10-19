using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ProtectedLoopControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedValueLeavesTheEarlierWriteAndRunsProtectedCleanup(bool asynchronous)
    {
        const string source = """
            with open('out.txt', 'w') as target:
                for i in range(2):
                    target.write('before')
                    target.flush()
                    target.write('x' * (16777216 + i))
                    break
            """;
        var budget = new LythonMemoryBudget(1048576);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) };
        using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(mock!, options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("before", delayed?.ReadText("/out.txt") ?? mock!.ReadText("/out.txt"));
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public async Task PendingBreakKeepsOwnershipUntilCancelledExitFinishes()
    {
        const string source = """
            for i in range(2):
                with open('out.txt', 'w') as target:
                    target.write('before')
                    target.flush()
                    break
            """;
        var budget = new LythonMemoryBudget(1048576);
        using var script = new LythonEngine().Compile(source, budget);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        var entered = host.PauseWriteUntilCancellation("/out.txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            if (!pending.IsCompleted)
            {
                using var cleanup = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> Programs()
    {
        foreach (var program in ProtectedLoopPrograms.Cases)
            foreach (var asynchronous in new[] { false, true })
                yield return [program.Name, program.Source, program.Output, program.Cleanup, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ProtectedJumpsPreservePythonCleanupAndOwnership(
        string name, string source, string output, string cleanup, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        var delayed = asynchronous ? new DelayedLythonHost() : null;
        var mock = asynchronous ? null : new MockLythonHost();
        if (delayed is not null) delayed.SeedFile("/cleanup.txt", "");
        else mock!.SeedFile("/cleanup.txt", "");
        using (var result = asynchronous
            ? await script.RunAsync(delayed!) : script.Run(mock!))
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(output, result.StandardOutput);
            Assert.Equal(cleanup, delayed?.ReadText("/cleanup.txt") ?? mock!.ReadText("/cleanup.txt"));
            if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
