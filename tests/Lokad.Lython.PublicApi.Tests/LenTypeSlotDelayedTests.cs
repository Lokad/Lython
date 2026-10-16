using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LenTypeSlotDelayedTests
{
    private const string Source = """
        class Sized:
            def __getattribute__(self, name):
                raise RuntimeError('ordinary lookup')
            @property
            def __len__(self):
                print('descriptor')
                with open('/bind') as marker:
                    marker.read()
                def size():
                    print('call')
                    with open('/call') as marker:
                        marker.read()
                    return 7
                return size
        print(len(Sized()))
        """;

    [Theory]
    [InlineData(null)]
    [InlineData("/bind")]
    [InlineData("/call")]
    public async Task DescriptorAndBodyAwaitWithFundingUntilCleanup(string? pausedPath)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(Source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/bind", "data");
        host.SeedFile("/call", "data");
        var entered = pausedPath is null ? null : host.PauseReadUntilCancellation(pausedPath);
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Same(entered, first);
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(pausedPath is null, result.Success);
            Assert.Equal(pausedPath is null ? "descriptor\ncall\n7\n" :
                pausedPath == "/bind" ? "descriptor\n" : "descriptor\ncall\n", result.StandardOutput);
            if (pausedPath is null) Assert.True(host.CompletedAsynchronously >= 2);
            else Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
