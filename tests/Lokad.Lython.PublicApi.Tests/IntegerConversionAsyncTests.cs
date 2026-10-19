using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerConversionAsyncTests
{
    [Theory]
    [InlineData("int", null)]
    [InlineData("int", "/bind")]
    [InlineData("int", "/call")]
    [InlineData("index", null)]
    [InlineData("index", "/bind")]
    [InlineData("index", "/call")]
    [InlineData("base", null)]
    [InlineData("base", "/bind")]
    [InlineData("base", "/call")]
    [InlineData("trunc", null)]
    [InlineData("trunc", "/bind")]
    [InlineData("trunc", "/call")]
    [InlineData("trunc-index", null)]
    [InlineData("trunc-index", "/bind")]
    [InlineData("trunc-index", "/call")]
    [InlineData("operator-index", null)]
    [InlineData("operator-index", "/bind")]
    [InlineData("operator-index", "/call")]
    public async Task DescriptorAndBodyAwaitWithFundingUntilCleanup(string route, string? pausedPath)
    {
        var slot = route == "int" ? "__int__" : route == "trunc" ? "__trunc__" : "__index__";
        var source = Source.Replace("CONVERSION_SLOT", slot).Replace("CONVERSION_VALUE", route == "base" ? "2" : "7");
        if (route == "operator-index")
            source = "import operator\n" + source.Replace("raise RuntimeError('ordinary lookup')", "return object.__getattribute__(self, name)");
        source += route == "trunc-index"
            ? "\nclass Value:\n def __trunc__(self): return Converted()\nprint(int(Value()))"
            : route == "operator-index" ? "\nprint(operator.index(Converted()))"
            : route == "base" ? "\nprint(int('11', Converted()))" : "\nprint(int(Converted()))";
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
            Assert.Equal(pausedPath is null ? "descriptor\ncall\n" + (route == "base" ? "3\n" : "7\n") :
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
            def __getattribute__(self, name):
                raise RuntimeError('ordinary lookup')
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
