using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class WildcardCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MatchingUsesFundedInputsWithoutAdmittingScratch(bool cancelled)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var name = PyString.FromString(new string('κ', 1000), governor);
            var pattern = PyString.FromString("*[α-ω]?", governor);
            var before = budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            if (cancelled) cancellation.Cancel();
            Assert.True(budget.TryReserve(limit - before, out var blocker));
            using (blocker)
            {
                if (cancelled)
                    Assert.ThrowsAny<OperationCanceledException>(() => LythonRuntime.FnMatchModule.MatchSimple(name, pattern, cancellation.Token));
                else
                    Assert.True(LythonRuntime.FnMatchModule.MatchSimple(name, pattern, cancellation.Token));
                Assert.Equal(limit, budget.ReservedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                Assert.Null(governor.AdmissionFailure);
            }
            Assert.Equal(before, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task NativeWildcardBacktrackingObservesCancellation()
    {
        var name = PyString.FromString(new string('a', 20_000));
        var pattern = PyString.FromString("*" + new string('a', 10_000) + "b");
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
