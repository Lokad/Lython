using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FloatConversionAsyncTests
{
    [Theory]
    [InlineData("float", null)]
    [InlineData("float", "/bind")]
    [InlineData("float", "/call")]
    [InlineData("index", null)]
    [InlineData("index", "/bind")]
    [InlineData("index", "/call")]
    public async Task DescriptorAndBodyAwaitWithFundingUntilCleanup(string route, string? pausedPath)
    {
        var slot = route == "float" ? "__float__" : "__index__";
        var source = Source.Replace("CONVERSION_SLOT", slot).Replace("CONVERSION_VALUE", route == "float" ? "7.0" : "7")
            + "\nprint(float(Converted()))";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Conversion finished before the controlled host operation.");
                }
                Assert.Same(entered, first);
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            if (pausedPath is null) Assert.True(result.Success, result.Failure?.Message);
            else Assert.False(result.Success);
            Assert.Equal(pausedPath is null ? "descriptor\ncall\n7.0\n" :
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

    private const string Source = """
        class Converted:
            @property
            def CONVERSION_SLOT(self):
                print('descriptor')
                with open('/bind') as marker:
                    marker.read()
                def convert():
                    print('call')
                    with open('/call') as marker:
                        marker.read()
                    return CONVERSION_VALUE
                return convert
        """;
}
