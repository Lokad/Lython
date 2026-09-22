using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N01 public boundary: async structural comparisons with suspending __eq__
// complete, stay isolated across runs, and preserve cyclic rejection.
// Ownership (no worker roots) is pinned white-box; here we pin observable
// behavior through the built library in both modes where applicable.
public sealed class AsyncStructuralGuardIsolationTests
{
    private const string DelayedEqualityScript =
        "from pathlib import Path\n" +
        "class E:\n" +
        "    def __eq__(self, other):\n" +
        "        return Path(\"/d.txt\").read_text() == \"x\\n\"\n" +
        "a = [E(), [1, 2]]\n" +
        "b = [E(), [1, 2]]\n" +
        "assert a == b\n" +
        "assert not (a != b)\n" +
        "print(\"ok\")\n";

    private static DelayedLythonHost SeedDelayedHost()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/d.txt", "x\n");
        return host;
    }

    [Fact]
    public async Task DelayedListEquality_CompletesAsync()
    {
        var script = new LythonEngine().Compile(DelayedEqualityScript);
        Assert.True(script.IsValid);
        var host = SeedDelayedHost();
        var result = await script.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ok\n", result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task IndependentDelayedRuns_DoNotContaminate()
    {
        var script = new LythonEngine().Compile(DelayedEqualityScript);
        Assert.True(script.IsValid);

        for (var i = 0; i < 3; i++)
        {
            var host = SeedDelayedHost();
            var result = await script.RunAsync(host);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("ok\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task InterleavedDelayedRuns_BothSucceed()
    {
        var script = new LythonEngine().Compile(DelayedEqualityScript);
        Assert.True(script.IsValid);
        var hostA = SeedDelayedHost();
        var hostB = SeedDelayedHost();
        var t1 = script.RunAsync(hostA);
        var t2 = script.RunAsync(hostB);
        var results = await Task.WhenAll(t1, t2);
        foreach (var r in results)
        {
            Assert.True(r.Success, r.Failure?.Message);
            Assert.Equal("ok\n", r.StandardOutput);
        }
    }

    [Fact]
    public async Task CyclicLists_WithDelayedEq_StillRaiseRecursionError()
    {
        const string cyclic =
            "from pathlib import Path\n" +
            "class E:\n" +
            "    def __eq__(self, other):\n" +
            "        return Path(\"/d.txt\").read_text() == \"x\\n\"\n" +
            "a = []\n" +
            "b = []\n" +
            "a.append(a)\n" +
            "b.append(b)\n" +
            "print(a == b)\n";
        var script = new LythonEngine().Compile(cyclic);
        var host = SeedDelayedHost();
        var result = await script.RunAsync(host);
        Assert.False(result.Success);
        Assert.Equal("RecursionError", result.Failure?.ExceptionType);

        var healthy = new LythonEngine().Compile(DelayedEqualityScript);
        var ok = await healthy.RunAsync(SeedDelayedHost());
        Assert.True(ok.Success, ok.Failure?.Message);
    }

    [Fact]
    public void SyncDelayedRun_FailsFastWithGuidance()
    {
        var script = new LythonEngine().Compile(DelayedEqualityScript);
        Assert.True(script.IsValid);
        var result = script.Run(SeedDelayedHost());
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("use RunAsync", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlainListEquality_MatchesSyncAndAsync()
    {
        const string plain = "a = [[1, 2], [3]]\nb = [[1, 2], [3]]\nprint(a == b)\nprint(a != b)\n";
        var script = new LythonEngine().Compile(plain);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("True\nFalse\n", sync.StandardOutput);
    }

    [Fact]
    public async Task PlainListEquality_MatchesAsync()
    {
        const string plain = "a = [[1, 2], [3]]\nb = [[1, 2], [3]]\nprint(a == b)\nprint(a != b)\n";
        var script = new LythonEngine().Compile(plain);
        Assert.True(script.IsValid);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("True\nFalse\n", asyncResult.StandardOutput);
    }
}
