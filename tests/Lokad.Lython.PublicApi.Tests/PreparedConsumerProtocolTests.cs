using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedConsumerProtocolTests
{
    [Theory]
    [InlineData("__bool__", false)]
    [InlineData("__bool__", true)]
    [InlineData("__getattr__", false)]
    [InlineData("__getattr__", true)]
    [InlineData("__iter__", false)]
    [InlineData("__iter__", true)]
    public async Task BorrowedInputsSurviveSuspendingProtocolsAndAwaitedCancellation(string protocol, bool cancel)
    {
        var parameter = protocol == "__getattr__" ? "self,name" : "self";
        var returned = protocol == "__bool__" ? "True" : protocol == "__iter__" ? "iter(('selected',))" : "'selected'";
        var consumer = protocol == "__bool__" ? "if value:print('selected')"
            : protocol == "__iter__" ? "for item in value:print(item)" : "print(value.payload)";
        var source = "class Hook:\n def " + protocol + "(" + parameter + "):\n  with open('/gate','a') as output:output.write('done')\n  return " + returned
            + "\ndef consume(value):\n " + consumer + "\nconsume(Hook())";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/gate", "before:");
            var entered = cancel ? host.PauseWriteUntilCancellation("/gate") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                    Assert.Same(entered, first);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.Equal(!cancel, result.Success);
                Assert.Equal(cancel ? "" : "selected\n", result.StandardOutput);
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal(cancel ? "before:" : "before:done", host.ReadText("/gate"));
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
