using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HtmlEscapeDelayedTests
{
    private const string Source = """
        import html
        class Quote:
            def __bool__(self):
                print('truth')
                with open('/quote') as marker:
                    return marker.read() == 'yes'
        class Text:
            @property
            def replace(self):
                with open('/lookup') as marker:
                    marker.read()
                print('lookup')
                return self.change
            def change(self,old,new):
                print('replace',repr(old))
                with open('/replace') as marker:
                    marker.read()
                return self
        text=Text()
        print(html.escape(text,Quote()) is text)
        """;

    [Fact]
    public async Task ReplaceLookupsCallsAndQuoteTruthAwaitInPythonOrder()
    {
        var script = Compile();
        var syncHost = new MockLythonHost();
        foreach (var path in new[] { "/lookup", "/replace", "/quote" }) syncHost.SeedFile(path, "yes");
        var sync = script.Run(syncHost);
        var host = Host();
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("lookup\nreplace '&'\nlookup\nreplace '<'\nlookup\nreplace '>'\ntruth\nlookup\nreplace '\"'\nlookup\nreplace \"'\"\nTrue\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 11);
    }

    [Fact]
    public async Task QuoteCancellationStopsBeforeLaterReplaceCalls()
    {
        var script = Compile();
        var host = Host();
        var started = host.PauseReadUntilCancellation("/quote");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal("lookup\nreplace '&'\nlookup\nreplace '<'\nlookup\nreplace '>'\ntruth\n", result.StandardOutput);
    }

    private static DelayedLythonHost Host()
    {
        var host = new DelayedLythonHost();
        foreach (var path in new[] { "/lookup", "/replace", "/quote" }) host.SeedFile(path, "yes");
        return host;
    }

    private static LythonCompiledScript Compile()
    {
        var script = new LythonEngine().Compile(Source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
