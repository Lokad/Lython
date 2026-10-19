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
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Task.Run(() =>
        {
            entered.SetResult();
            return LythonRuntime.FnMatchModule.MatchSimple(name, pattern, cancellation.Token);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            await cancellation.CancelAsync();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
    }
}
