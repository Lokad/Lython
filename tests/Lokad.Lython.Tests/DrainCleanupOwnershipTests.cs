using System.IO;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class DrainCleanupOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedDrainKeepsFundingUntilPendingCleanupFinishes(bool cancelled)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var baseline = budget.ReservedBytes;
            var source = new PendingCleanupSource(cancelled, cancellation.Token);
            var pending = PyIteration.DrainLeasedAsync(source, new(0, 0, 0, 0), context).AsTask();
            try
            {
                if (cancelled)
                {
                    await source.PendingPull.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    cancellation.Cancel();
                }
                await source.CleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                source.FinishCleanup.SetResult();
                if (cancelled)
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
                else
                    await Assert.ThrowsAsync<IOException>(async () => await pending);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            finally
            {
                cancellation.Cancel();
                source.FinishCleanup.TrySetResult();
                try { using var result = await pending; }
                catch (Exception) { /* Join the deliberately failed user before cleanup. */ }
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PendingCleanupSource(bool cancelled, CancellationToken token)
        : IAsyncEnumerable<object>, IAsyncEnumerator<object>
    {
        private bool _yielded;
        public TaskCompletionSource PendingPull { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CleanupStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FinishCleanup { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Current { get; } = new();
        public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;

        public async ValueTask<bool> MoveNextAsync()
        {
            if (!_yielded) { _yielded = true; return true; }
            if (cancelled)
            {
                PendingPull.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            throw new InvalidOperationException("iteration failed");
        }

        public async ValueTask DisposeAsync()
        {
            CleanupStarted.SetResult();
            await FinishCleanup.Task;
            if (!cancelled) throw new IOException("cleanup failed");
        }
    }
}
