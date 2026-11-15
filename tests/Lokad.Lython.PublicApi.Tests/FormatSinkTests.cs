using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormatSinkTests
{
    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    [InlineData(false, "refuse")]
    [InlineData(true, "refuse")]
    public async Task PendingFieldKeepsStorageFundedAndJoinsBeforeReuse(bool mapping, string outcome)
    {
        var expression = mapping ? "'begin|{value:>9}|end'.format_map({'value':value})"
            : "'begin|{0:>9}|end'.format(value)";
        var source = """
            import time
            class Field:
             def __format__(self,spec):
              with open('data.txt') as stream:
               value=stream.read()
               time.sleep(0)
               return value+spec
            """ + "\ndef render(value):return " + expression + "\nrender('warm')\ntry:\n result=render(Field())\nexcept BaseException:print('caught')\nprint('done')\nreturn result";
        await AssertHeldOperation(source, outcome, ">9");
    }

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    [InlineData(false, "refuse")]
    [InlineData(true, "refuse")]
    public async Task PendingMappingOrSuffixLookupKeepsItsOwnerUntilCleanup(bool attribute, string outcome)
    {
        const string setup = """
            import time
            def read_value():
             with open('data.txt') as stream:
              value=stream.read()
              time.sleep(0)
              return value+'!'
            class Mapping:
             def __getitem__(self,key):return read_value()
            class Warm:value='warm'
            class Source:
             @property
             def value(self):return read_value()
            """;
        var selection = attribute
            ? "render='begin|{0.value}|end'.format\nrender(Warm())\ntarget=Source()"
            : "render='begin|{value}|end'.format_map\nrender({'value':'warm'})\ntarget=Mapping()";
        var source = setup + "\n" + selection + "\ntry:\n result=render(target)\nexcept BaseException:print('caught')\nprint('done')\nreturn result";
        await AssertHeldOperation(source, outcome, "!");
    }

    private static async Task AssertHeldOperation(string source, string outcome, string suffix)
    {
        const long limit = 2 * 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        var timing = new HeldTiming();
        var host = new DelayedLythonHost();
        var payload = new string('x', 4096);
        host.SeedFile("/data.txt", payload);
        host.SetTiming(timing);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            ILythonMemoryReservation? fill = null;
            try
            {
                var first = await Task.WhenAny(timing.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Same(timing.Entered.Task, first);
                Assert.False(pending.IsCompleted);
                Assert.Equal(cancellation.Token, timing.SeenToken);
                var suspendedBalance = budget.ReservedBytes;
                Assert.True(suspendedBalance > baseline);
                if (outcome == "cancel") cancellation.Cancel();
                if (outcome == "refuse") Assert.True(budget.TryReserve(limit - budget.ReservedBytes, out fill));
                // The active user must settle before its input/output and scratch
                // are refunded, including after cancellation has been requested.
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes >= suspendedBalance);
                timing.Release.TrySetResult();
                using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(5)))
                {
                    if (outcome == "success")
                    {
                        Assert.True(result.Success, result.Failure?.Message);
                        Assert.Equal("done\n", result.StandardOutput);
                        Assert.Equal("begin|" + payload + suffix + "|end", result.ReturnValue);
                        Assert.True(budget.ReservedBytes > baseline);
                    }
                    else
                    {
                        Assert.False(result.Success);
                        Assert.Equal(outcome == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                            result.FailureAttribution?.Reason);
                        Assert.Empty(result.StandardOutput);
                    }
                }
            }
            finally
            {
                timing.Release.TrySetResult();
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                fill?.Dispose();
            }
            Assert.True(host.ClosedFileCount > 0);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class HeldTiming : ILythonTiming
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken SeenToken;
        public long MonotonicNanoseconds => 0;
        public async ValueTask DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            SeenToken = cancellationToken;
            Entered.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
