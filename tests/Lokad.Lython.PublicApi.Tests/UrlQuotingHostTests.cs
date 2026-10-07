using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UrlQuotingHostTests
{
    private const string SafeSource = """
        import urllib.parse as p
        from pathlib import Path
        class Safe:
            def __iter__(self):
                self.done=False
                return self
            def __next__(self):
                if self.done:
                    raise StopIteration
                self.done=True
                return int(Path('/safe').read_text())
        print(p.quote_from_bytes(b'/\xff',safe=Safe()))
        """;

    [Fact]
    public async Task SafeIteratorSuspendsForMediatedHostReads()
    {
        var script = Compile(SafeSource);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/safe", "47");
        var host = new DelayedLythonHost();
        host.SeedFile("/safe", "47");
        var sync = script.Run(syncHost);
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("/%FF\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task CancellationStopsDuringSafeIteration()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/safe", "47");
        var started = host.PauseReadUntilCancellation("/safe");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Compile(SafeSource).RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal("", result.StandardOutput);
    }

    [Theory]
    [InlineData("quote_from_bytes(b'\\xff'*20)", false)]
    [InlineData("unquote(b'\\xff'*20)", true)]
    public async Task LimitsCountOutputScalars(string expression, bool success)
    {
        var script = Compile("from urllib.parse import quote_from_bytes,unquote\nprint(" + expression + ")");
        var options = new LythonRunOptions { MaxStringLength = 20, MaxCollectionSize = 20 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success == success, result.Failure?.Message);
            Assert.Equal(success ? new string('�', 20) + "\n" : "", result.StandardOutput);
            if (!success) Assert.Contains("maximum string length", result.Failure?.Message);
        }
    }

    private static LythonCompiledScript Compile(string source)
    {
        var result = new LythonEngine().Compile(source);
        Assert.True(result.IsValid, string.Join("; ", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return result;
    }
}
