using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TruthTypeSlotDelayedTests
{
    private static string Source(string slot, bool conditional) => "class Answer:\n    def __getattribute__(self,name):\n        raise RuntimeError('ordinary lookup')\n    @property\n    def " + slot + "(self):\n        print('descriptor')\n        with open('/descriptor') as marker:\n            marker.read()\n        def evaluate():\n            print('call')\n            with open('/call') as marker:\n                value=marker.read()\n            return " + (slot == "__bool__" ? "value == 'yes'" : "0") + "\n        return evaluate\n" + (conditional ? "if Answer():\n    print('True')\nelse:\n    print('False')\n" : "print(bool(Answer()))\n");

    [Theory]
    [InlineData("__bool__", "True", false)]
    [InlineData("__len__", "False", false)]
    [InlineData("__bool__", "True", true)]
    [InlineData("__len__", "False", true)]
    public async Task TruthDescriptorAndCallAwaitWithoutOrdinaryLookup(string slot, string expected, bool conditional)
    {
        using var script = Compile(slot, conditional);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/descriptor", "yes");
        syncHost.SeedFile("/call", "yes");
        using var sync = script.Run(syncHost);
        var host = Host();
        using var result = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("descriptor\ncall\n" + expected + "\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 2);
    }

    [Theory]
    [InlineData("__bool__", "/descriptor", "descriptor\n", false)]
    [InlineData("__bool__", "/call", "descriptor\ncall\n", false)]
    [InlineData("__len__", "/descriptor", "descriptor\n", false)]
    [InlineData("__len__", "/call", "descriptor\ncall\n", false)]
    [InlineData("__bool__", "/descriptor", "descriptor\n", true)]
    [InlineData("__bool__", "/call", "descriptor\ncall\n", true)]
    [InlineData("__len__", "/descriptor", "descriptor\n", true)]
    [InlineData("__len__", "/call", "descriptor\ncall\n", true)]
    public async Task CancellationStopsBeforeTheTruthResult(string slot, string path, string expected, bool conditional)
    {
        var host = Host();
        var started = host.PauseReadUntilCancellation(path);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var script = Compile(slot, conditional);
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        try
        {
            await started.WaitAsync(cancellation.Token);
            Assert.False(pending.IsCompleted);
            cancellation.Cancel();
            using var result = await pending;
            Assert.False(result.Success);
            Assert.Contains("canceled", result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private static DelayedLythonHost Host()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/descriptor", "yes");
        host.SeedFile("/call", "yes");
        return host;
    }

    private static LythonCompiledScript Compile(string slot, bool conditional)
    {
        var script = new LythonEngine().Compile(Source(slot, conditional));
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
