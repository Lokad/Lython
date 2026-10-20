using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BytesJoinExecutionTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task InputTailEffectsAndErrorsPrecedeItemValidation(bool asynchronous, bool tailError)
    {
        using var script = new LythonEngine().Compile("""
            def parts():
                try:
                    yield 1
                    print('tail')
                    TAIL
                finally:
                    print('closed')
            try:
                b''.join(parts())
            except Exception as error:
                print(type(error).__name__)
            """.Replace("TAIL", tailError ? "raise ValueError('tail')" : "yield b'a'"));
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(65536);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("tail\nclosed\n" + (tailError ? "ValueError\n" : "TypeError\n"), result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("success")] [InlineData("cancel")] [InlineData("refusal")]
    public async Task SuspendedInputRemainsFundedUntilHostAndIteratorCleanupComplete(string route)
    {
        var budget = new LythonMemoryBudget(524288);
        using var script = new LythonEngine().Compile("""
            import time
            def parts():
                try:
                    yield b'ab'
                    time.sleep(1)
                    yield b'cd'
                finally:
                    print('closed')
            return b'-'.join(parts())
            """);
        Assert.True(script.IsValid);
        using var cancellation = new CancellationTokenSource();
        var timing = new HeldTiming(budget);
        var host = new MockLythonHost();
        host.SetTiming(timing);
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        try
        {
            await timing.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
            if (route == "cancel")
            {
                cancellation.Cancel();
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > 0);
            }
            if (route == "refusal")
            {
                Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out var fill));
                timing.Fill = fill;
            }
            timing.Release.TrySetResult();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (route == "success")
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ab-cd", System.Text.Encoding.ASCII.GetString(Assert.IsType<byte[]>(result.ReturnValue)));
                Assert.Equal("closed\n", result.StandardOutput);
            }
            else Assert.Equal(route == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal(1, timing.Calls);
            Assert.True(timing.FundedUntilCompletion);
        }
        finally
        {
            timing.Release.TrySetResult();
            using var completed = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            timing.Fill?.Dispose();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class HeldTiming(LythonMemoryBudget budget) : ILythonTiming
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ILythonMemoryReservation? Fill;
        internal int Calls;
        internal bool FundedUntilCompletion;
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task; }
            finally { FundedUntilCompletion = budget.ReservedBytes > 0; }
        }
    }
}
