using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N15: functools consumers and wrappers compose through async suspension instead
// of failing with synchronous-host guidance. Reduce awaits iteration and combiner
// dispatch; partial awaits its target with shared argument-combining facts; the
// cache wrapper awaits its target on misses with identical hit/miss accounting.
// Synchronous runs keep failing fast with RunAsync guidance instead of wedging.
public sealed class FunctoolsAsyncCompositionTests
{
    private static DelayedLythonHost SeedDelayedHost()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/r.txt", "3\n1\n4\n1\n5\n");
        host.SeedFile("/d.txt", "2020-01-02");
        return host;
    }

    private static MockLythonHost SeedSyncHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/r.txt", "3\n1\n4\n1\n5\n");
        host.SeedFile("/d.txt", "2020-01-02");
        return host;
    }

    private static async Task AssertAsyncMatchesSync(string source, object? expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(SeedSyncHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = SeedDelayedHost();
        var asyncResult = await script.RunAsync(delayed);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(delayed.CompletedAsynchronously > 0);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ReduceOverSuspendingIteratorComposes()
    {
        await AssertAsyncMatchesSync(
            "import functools\nwith open(\"/r.txt\") as f:\n    return functools.reduce(lambda a, b: a + int(b), f, 0)\n",
            new BigInteger(14));
    }

    [Fact]
    public async Task ReduceWithSuspendingCallbackComposes()
    {
        await AssertAsyncMatchesSync(
            "import functools\nfrom pathlib import Path\ndef add(a, b):\n    return a + int(b) + len(Path(\"/d.txt\").read_text())\nwith open(\"/r.txt\") as f:\n    return functools.reduce(add, f, 0)\n",
            new BigInteger(64));
    }

    [Fact]
    public async Task ReduceEmptyWithoutInitializerStillRejects()
    {
        var script = new LythonEngine().Compile(
            "import functools\nreturn functools.reduce(lambda a, b: a + b, [])\n");
        Assert.True(script.IsValid);
        var sync = script.Run(SeedSyncHost());
        Assert.False(sync.Success);
        Assert.Equal("TypeError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(SeedDelayedHost());
        Assert.False(asyncResult.Success);
        Assert.Equal("TypeError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CachedSuspendingTargetComposes()
    {
        await AssertAsyncMatchesSync(
            "import functools\nfrom pathlib import Path\n@functools.cache\ndef read(p):\n    return Path(p).read_text()\na = read(\"/d.txt\")\nb = read(\"/d.txt\")\nreturn [a, b, read.cache_info().hits]\n",
            new List<object?> { "2020-01-02", "2020-01-02", new BigInteger(1) });
    }

    [Fact]
    public async Task CachedRecursiveSuspendingTargetComposes()
    {
        await AssertAsyncMatchesSync(
            "import functools\nfrom pathlib import Path\n@functools.cache\ndef depth(n):\n    text = Path(\"/d.txt\").read_text()\n    return len(text) if n < 2 else depth(n - 1)\nreturn [depth(3), depth(3)]\n",
            new List<object?> { new BigInteger(10), new BigInteger(10) });
    }

    [Fact]
    public async Task PartialOverSuspendingTargetComposes()
    {
        await AssertAsyncMatchesSync(
            "import functools\nfrom pathlib import Path\ndef get(p, suffix):\n    return Path(p).read_text() + suffix\ngetd = functools.partial(get, \"/d.txt\", suffix=\"!\")\nreturn [getd(), getd(suffix=\"?\")]\n",
            new List<object?> { "2020-01-02!", "2020-01-02?" });
    }

    [Theory]
    [InlineData("import functools\nwith open(\"/r.txt\") as f:\n    return functools.reduce(lambda a, b: a + int(b), f, 0)\n")]
    [InlineData("import functools\nfrom pathlib import Path\n@functools.cache\ndef read(p):\n    return Path(p).read_text()\nreturn read(\"/d.txt\")\n")]
    [InlineData("import functools\nfrom pathlib import Path\ndef get(p):\n    return Path(p).read_text()\nreturn functools.partial(get)(\"/d.txt\")\n")]
    public void SyncRunsFailFastWithRunAsyncGuidance(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var result = script.Run(SeedDelayedHost());
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("use RunAsync", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancelledReduceReportsCancellation()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var script = new LythonEngine().Compile(
            "import functools\nwith open(\"/r.txt\") as f:\n    return functools.reduce(lambda a, b: a + int(b), f, 0)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { CancellationToken = cancelled.Token };
        var asyncResult = await script.RunAsync(SeedDelayedHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Contains("execution canceled", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }
}
