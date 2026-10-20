using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class WildcardCancellationTests
{
    [Fact]
    public async Task NativeWildcardBacktrackingObservesCancellation()
    {
        var name = PyString.FromString(new string('a', 20_000));
        var pattern = PyString.FromString("*" + new string('a', 10_000) + "b");
        _ = name.GetRunes();
        _ = pattern.GetRunes();
        using var cancellation = new CancellationTokenSource();
        using var entered = new ManualResetEventSlim();
        var observedEntry = false;
        // The matching worker and cancellation clock must not queue behind the
        // suite's thread-pool work. Arm cancellation independently of its async
        // continuation, which can otherwise resume after matching has ended.
        var controller = new Thread(() =>
        {
            observedEntry = entered.Wait(TimeSpan.FromSeconds(5));
            if (observedEntry) Thread.Sleep(20);
            cancellation.Cancel();
        }) { IsBackground = true };
        controller.Start();
        var pending = Task.Factory.StartNew(() =>
        {
            entered.Set();
            return LythonRuntime.FnMatchModule.MatchSimple(name, pattern, cancellation.Token);
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(controller.Join(TimeSpan.FromSeconds(5)), "Cancellation controller did not finish.");
            Assert.True(observedEntry, "The matching worker never started.");
        }
        finally
        {
            cancellation.Cancel();
            entered.Set();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
            finally
            {
                Assert.True(controller.Join(TimeSpan.FromSeconds(5)), "Cancellation controller cleanup did not finish.");
            }
        }
    }
}
