using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HtmlUnescapeDelayedTests
{
    private const string Source = """
        import html
        class Answer:
            def __bool__(self):
                print('truth')
                with open('/truth') as marker:
                    return marker.read() == 'yes'
        class Text:
            def __contains__(self,needle):
                print('contains',repr(needle))
                with open('/contains') as marker:
                    marker.read()
                return Answer()
        text=Text()
        try:
            print(html.unescape(text) is text)
        except TypeError:
            print('TypeError')
        """;

    [Theory]
    [InlineData("no", "contains '&'\ntruth\nTrue\n")]
    [InlineData("yes", "contains '&'\ntruth\nTypeError\n")]
    public async Task ContainmentAndItsTruthSlotAwaitBeforeTextValidation(string answer, string expected)
    {
        var script = Compile();
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/contains", "go");
        syncHost.SeedFile("/truth", answer);
        var sync = script.Run(syncHost);
        var host = Host(answer);
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected, sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 2);
    }

    [Theory]
    [InlineData("/contains", "contains '&'\n")]
    [InlineData("/truth", "contains '&'\ntruth\n")]
    public async Task CancellationStopsBeforeLaterTruthOrResultValidation(string path, string expected)
    {
        var script = Compile();
        var host = Host("yes");
        var started = host.PauseReadUntilCancellation(path);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }

    private static DelayedLythonHost Host(string answer)
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/contains", "go");
        host.SeedFile("/truth", answer);
        return host;
    }

    private static LythonCompiledScript Compile()
    {
        var script = new LythonEngine().Compile(Source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
