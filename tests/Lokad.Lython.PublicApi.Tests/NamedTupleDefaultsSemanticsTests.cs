using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NamedTupleDefaultsSemanticsTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task DefaultsTailEffectsAndErrorsPrecedeExcessLength(bool asynchronous, bool tailError)
    {
        using var script = new LythonEngine().Compile("""
            import collections
            def defaults():
                try:
                    yield 1
                    yield 2
                    print('tail')
                    TAIL
                finally:
                    print('closed')
            try:
                collections.namedtuple('P', ['x'], defaults=defaults())
            except Exception as error:
                print(type(error).__name__)
            """.Replace("TAIL", tailError ? "raise ValueError('tail error')" : "yield 3"));
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("tail\nclosed\n" + (tailError ? "ValueError\n" : "TypeError\n"), result.StandardOutput);
    }

    [Theory]
    [InlineData(false, "'P', ['bad-name']")]
    [InlineData(true, "'P', ['bad-name']")]
    [InlineData(false, "'bad-name', ['x']")]
    [InlineData(true, "'bad-name', ['x']")]
    public async Task InvalidNamesDoNotExecuteDefaults(bool asynchronous, string arguments)
    {
        using var script = new LythonEngine().Compile("import collections\ndef defaults():\n    print('defaults ran')\n    yield 1\n"
            + "try:\n    collections.namedtuple(" + arguments + ", defaults=defaults())\nexcept ValueError:\n    print('invalid')\n");
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("invalid\n", result.StandardOutput);
    }

    [Fact]
    public async Task ExcessDefaultsAwaitHostCancellationAndCleanupBeforeReturningAllowance()
    {
        var budget = new LythonMemoryBudget(524288);
        using var script = new LythonEngine().Compile("""
            import collections, time
            def defaults():
                yield 1
                yield 2
                time.sleep(1)
                yield 3
            collections.namedtuple('P', ['x'], defaults=defaults())
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        var timing = new HeldTiming(budget, baseline);
        var host = new MockLythonHost();
        host.SetTiming(timing);
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await timing.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
        }
        finally
        {
            timing.Release.TrySetResult();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        Assert.True(timing.FundedUntilCompletion);
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class HeldTiming(LythonMemoryBudget budget, long baseline) : ILythonTiming
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool FundedUntilCompletion;
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            try { await Release.Task.WaitAsync(cancellationToken); }
            finally { FundedUntilCompletion = budget.ReservedBytes > baseline; }
        }
    }
}
