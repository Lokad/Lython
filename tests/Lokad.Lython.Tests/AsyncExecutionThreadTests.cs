using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class AsyncExecutionThreadTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task StartingThreadExitsWhileRunRemainsPending()
    {
        var entered = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<LythonExecutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = LythonExecutionResult.Succeeded(null, string.Empty, string.Empty, []);
        var pending = LythonRuntime.RunAsyncOnDedicatedStack(() =>
        {
            entered.SetResult(Thread.CurrentThread);
            return release.Task;
        });
        try
        {
            var worker = await entered.Task.WaitAsync(Watchdog);
            Assert.True(worker.Join(TimeSpan.FromSeconds(3)), "Async startup thread remained parked after its prefix returned.");
            Assert.False(pending.IsCompleted);
        }
        finally
        {
            release.TrySetResult(expected);
            Assert.Same(expected, await pending.WaitAsync(Watchdog));
        }
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
}
