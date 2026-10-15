using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class AsyncExecutionThreadTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task SuspendedRunReleasesWorkerForAnotherPrefixAndDisposal()
    {
        using var threads = new ExecutionThreads(16 * 1024 * 1024, 1, TimeSpan.FromSeconds(30));
        var entered = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<LythonExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = LythonExecutionResult.Succeeded(null, string.Empty, string.Empty, []);
        var pending = threads.RunAsync(() =>
        {
            entered.SetResult(Thread.CurrentThread);
            return release.Task;
        });
        try
        {
            var worker = await entered.Task.WaitAsync(Watchdog);
            // The prefix signals before returning. Wait for the worker's idle
            // monitor, rather than racing its return to the pool.
            Assert.True(SpinWait.SpinUntil(() => (worker.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(3)), "Async startup worker did not become idle after its prefix returned.");
            Thread? next = null;
            Assert.Same(expected, await threads.RunAsync(() =>
            {
                next = Thread.CurrentThread;
                return Task.FromResult(expected);
            }).WaitAsync(Watchdog));
            Assert.Same(worker, next);
            Assert.False(pending.IsCompleted);
            threads.Dispose();
            Assert.True(worker.Join(Watchdog));
            Assert.Throws<ObjectDisposedException>(() => { _ = threads.RunAsync(() => Task.FromResult(expected)); });
        }
        finally
        {
            release.TrySetResult(expected);
            Assert.Same(expected, await pending.WaitAsync(Watchdog));
        }
    }

    [Fact]
    public async Task SchedulingDoesNotWaitForCpuPrefix()
    {
        using var threads = new ExecutionThreads(16 * 1024 * 1024, 1, TimeSpan.FromSeconds(30));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var expected = LythonExecutionResult.Succeeded(null, string.Empty, string.Empty, []);
        // Keep a regressed blocking scheduler off the test thread, so cleanup
        // can release its prefix even when the scheduling assertion fails.
        var scheduled = Task.Factory.StartNew(() => threads.RunAsync(() =>
        {
            entered.Set();
            Assert.True(release.Wait(Watchdog));
            return Task.FromResult(expected);
        }), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            Assert.True(entered.Wait(Watchdog));
            var pending = await scheduled.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(pending.IsCompleted);
        }
        finally
        {
            release.Set();
            Assert.Same(expected, await (await scheduled.WaitAsync(Watchdog)).WaitAsync(Watchdog));
        }
    }

    [Fact]
    public async Task SynchronousAndAsynchronousPrefixesReuseTheSameWorker()
    {
        using var threads = new ExecutionThreads(16 * 1024 * 1024, 1, TimeSpan.FromSeconds(30));
        Thread? first = null, second = null, third = null;
        var expected = threads.Run(() =>
        {
            first = Thread.CurrentThread;
            return LythonExecutionResult.Succeeded(null, string.Empty, string.Empty, []);
        });
        await threads.RunAsync(() => { second = Thread.CurrentThread; return Task.FromResult(expected); }).WaitAsync(Watchdog);
        threads.Run(() => { third = Thread.CurrentThread; return expected; });
        Assert.Same(first, second);
        Assert.Same(first, third);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PrefixAndTaskFailuresPreserveOriginalException(int phase)
    {
        var expected = new InvalidOperationException("original failure");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<LythonExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = LythonRuntime.RunAsyncOnDedicatedStack(() =>
        {
            entered.SetResult();
            return phase switch
            {
                0 => throw expected,
                1 => Task.FromException<LythonExecutionResult>(expected),
                _ => release.Task,
            };
        });
        await entered.Task.WaitAsync(Watchdog);
        if (phase == 2) release.SetException(expected);
        Assert.Same(expected, await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(Watchdog)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationPreservesFaultedRelayContract(bool delayed)
    {
        var release = new TaskCompletionSource<LythonExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var pending = LythonRuntime.RunAsyncOnDedicatedStack(() => delayed
            ? release.Task : Task.FromCanceled<LythonExecutionResult>(cancellation.Token));
        if (delayed) release.SetCanceled(cancellation.Token);
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(Watchdog));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.True(pending.IsFaulted);
    }
}
