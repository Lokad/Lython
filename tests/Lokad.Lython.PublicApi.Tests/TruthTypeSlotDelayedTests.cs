using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TruthTypeSlotDelayedTests
{
    private static string Source(string slot) => "class Answer:\n    def __getattribute__(self,name):\n        raise RuntimeError('ordinary lookup')\n    @property\n    def " + slot + "(self):\n        print('descriptor')\n        with open('/descriptor') as marker:\n            marker.read()\n        def evaluate():\n            print('call')\n            with open('/call') as marker:\n                value=marker.read()\n            return " + (slot == "__bool__" ? "value == 'yes'" : "0") + "\n        return evaluate\nprint(bool(Answer()))\n";

    [Theory]
    [InlineData("__bool__", "True")]
    [InlineData("__len__", "False")]
    public async Task TruthDescriptorAndCallAwaitWithoutOrdinaryLookup(string slot, string expected)
    {
        var script = Compile(slot);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/descriptor", "yes");
        syncHost.SeedFile("/call", "yes");
        var sync = script.Run(syncHost);
        var host = Host();
        var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("descriptor\ncall\n" + expected + "\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 2);
    }

    [Theory]
    [InlineData("__bool__", "/descriptor", "descriptor\n")]
    [InlineData("__bool__", "/call", "descriptor\ncall\n")]
    [InlineData("__len__", "/descriptor", "descriptor\n")]
    [InlineData("__len__", "/call", "descriptor\ncall\n")]
    public async Task CancellationStopsBeforeTheTruthResult(string slot, string path, string expected)
    {
        var host = Host();
        var started = host.PauseReadUntilCancellation(path);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = Compile(slot).RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal(expected, result.StandardOutput);
    }

    private static DelayedLythonHost Host()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/descriptor", "yes");
        host.SeedFile("/call", "yes");
        return host;
    }

    private static LythonCompiledScript Compile(string slot)
    {
        var script = new LythonEngine().Compile(Source(slot));
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
