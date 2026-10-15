using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class AsyncExecutionWorkerLifetimeTests
{
    private static readonly AsyncLocal<int?> Ambient = new();
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingHostOperationsReleaseStartupThreadsAndKeepRunContexts(bool cancel)
    {
        var previous = Ambient.Value;
        using var script = new LythonEngine().Compile("import time\ntime.sleep(1)\nprint('done')\nreturn 7");
        using var child = new LythonEngine().Compile("return 11");
        var timings = Enumerable.Range(0, 8).Select(_ => new HeldTiming()).ToArray();
        var calls = new List<Task<LythonExecutionResult>>();
        using var cancellation = new CancellationTokenSource();
        try
        {
            for (var i = 0; i < timings.Length; i++)
            {
                var index = i;
                Ambient.Value = index;
                var host = new MockLythonHost();
                host.SetTiming(timings[i]);
                var captured = false;
                host.OnStandardOutputWrite = () =>
                {
                    if (captured) return;
                    captured = true;
                    Assert.Equal(index, Ambient.Value);
                    using var nested = child.Run(new MockLythonHost());
                    Assert.Equal(new BigInteger(11), nested.ReturnValue);
                };
                calls.Add(script.RunAsync(host, cancellationToken: cancellation.Token));
                var worker = await timings[i].Started.Task.WaitAsync(Watchdog);
                Assert.True(SpinWait.SpinUntil(() => !worker.IsAlive ||
                    (worker.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(3)),
                    "Startup worker did not become available while host I/O was pending.");
            }
            var workers = await Task.WhenAll(timings.Select(t => t.Started.Task)).WaitAsync(Watchdog);
            Assert.Single(workers.Distinct());
            Assert.All(calls, call => Assert.False(call.IsCompleted));
        }
        finally
        {
            Ambient.Value = previous;
            if (cancel) cancellation.Cancel();
            foreach (var timing in timings) timing.Release.TrySetResult();
            var results = await Task.WhenAll(calls).WaitAsync(Watchdog);
            foreach (var result in results)
            {
                using var owned = result;
                if (cancel)
                {
                    Assert.False(result.Success);
                    Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
                }
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(new BigInteger(7), result.ReturnValue);
                }
            }
        }
    }

    private sealed class HeldTiming : ILythonTiming
    {
        internal TaskCompletionSource<Thread> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public long MonotonicNanoseconds => 0;

        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Started.SetResult(Thread.CurrentThread);
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
