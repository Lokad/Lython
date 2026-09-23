using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N28: cancellation after actual suspension. Each test parks one delayed-host
// read mid-operation (a deterministic barrier, never a sleep), cancels, then
// pins failure identity, worker completion and a later independent success.
public sealed class AsyncMidFlightCancellationTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static string Numbers200()
        => string.Concat(Enumerable.Range(0, 200).Select(static i => i + "\n"));

    private static DelayedLythonHost SeedHost((string Path, string Text)[] files)
    {
        var host = new DelayedLythonHost();
        foreach (var (path, text) in files)
        {
            host.SeedFile(path, text);
        }

        return host;
    }

    private static MockLythonHost SeedSyncHost((string Path, string Text)[] files)
    {
        var host = new MockLythonHost();
        foreach (var (path, text) in files)
        {
            host.SeedFile(path, text);
        }

        return host;
    }

    private static async Task<LythonExecutionResult> CancelMidReadAsync(
        string source,
        string pausedPath,
        (string Path, string Text)[] files)
    {
        var host = SeedHost(files);
        using var cancellation = new CancellationTokenSource();
        var readStarted = host.PauseReadUntilCancellation(pausedPath);
        var task = Compile(source).RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await readStarted.WaitAsync(Watchdog);
        cancellation.Cancel();
        return await task.WaitAsync(Watchdog);
    }

    private static void AssertCancelled(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
    }

    private static async Task<LythonExecutionResult> AssertRerunMatchesSync(
        string source,
        (string Path, string Text)[] files)
    {
        var script = Compile(source);
        var sync = script.Run(SeedSyncHost(files));
        Assert.True(sync.Success, sync.Failure?.Message);
        var rerun = await script.RunAsync(SeedHost(files));
        Assert.True(rerun.Success, rerun.Failure?.Message);
        Assert.Equal(sync.ReturnValue, rerun.ReturnValue);
        return rerun;
    }

    [Fact]
    public async Task ReduceOverSuspendingLinesCancelsMidFlight()
    {
        const string source = "import functools\nwith open(\"/data.txt\") as f:\n    return functools.reduce(lambda a, b: a + int(b), f, 0)\n";
        var files = new[] { ("/data.txt", Numbers200()) };
        AssertCancelled(await CancelMidReadAsync(source, "/data.txt", files));
        var rerun = await AssertRerunMatchesSync(source, files);
        Assert.Equal(new BigInteger(19900), rerun.ReturnValue);
    }

    [Fact]
    public async Task ListConstructorOverSuspendingLinesCancelsMidFlight()
    {
        const string source = "with open(\"/data.txt\") as f:\n    return list(f)\n";
        var files = new[] { ("/data.txt", Numbers200()) };
        AssertCancelled(await CancelMidReadAsync(source, "/data.txt", files));
        var rerun = await AssertRerunMatchesSync(source, files);
        Assert.Equal(200, ((List<object?>)rerun.ReturnValue!).Count);
    }

    [Fact]
    public async Task DictBuildLoopCancelsMidFlight()
    {
        const string source = "d = {}\nwith open(\"/data.txt\") as f:\n    for line in f:\n        d[line.strip()] = len(line)\nreturn len(d)\n";
        var files = new[] { ("/data.txt", Numbers200()) };
        AssertCancelled(await CancelMidReadAsync(source, "/data.txt", files));
        var rerun = await AssertRerunMatchesSync(source, files);
        Assert.Equal(new BigInteger(200), rerun.ReturnValue);
    }

    [Fact]
    public async Task StatisticsMeanOverSuspendingLinesCancelsMidFlight()
    {
        const string source = "import statistics\nwith open(\"/data.txt\") as f:\n    return statistics.mean([int(line) for line in f])\n";
        var files = new[] { ("/data.txt", Numbers200()) };
        AssertCancelled(await CancelMidReadAsync(source, "/data.txt", files));
        var rerun = await AssertRerunMatchesSync(source, files);
        Assert.Equal(99.5, Assert.IsType<double>(rerun.ReturnValue));
    }

    [Fact]
    public async Task PartialOverSuspendingTargetCancelsMidFlight()
    {
        const string source = "import functools\nfrom pathlib import Path\ndef get(p, suffix):\n    return Path(p).read_text() + suffix\ngetd = functools.partial(get, \"/gate.txt\", suffix=\"!\")\nreturn getd()\n";
        var files = new[] { ("/gate.txt", "G") };
        AssertCancelled(await CancelMidReadAsync(source, "/gate.txt", files));
        var rerun = await AssertRerunMatchesSync(source, files);
        Assert.Equal("G!", rerun.ReturnValue);
    }

    [Fact]
    public async Task CacheMissCancelledMidFlightDoesNotPublish()
    {
        // The cancelled miss releases its key charge instead of publishing (the same refund
        // path FailedCallsDoNotExhaust pins for sync failures); a fresh run then publishes
        // normally, proving the wrapper was neither poisoned nor retaining execution roots.
        const string source = "import functools\nfrom pathlib import Path\n@functools.cache\ndef read(p):\n    return Path(p).read_text()\nread(\"/gate.txt\")\n";
        var files = new[] { ("/gate.txt", "G") };
        AssertCancelled(await CancelMidReadAsync(source, "/gate.txt", files));
        const string republish = "import functools\nfrom pathlib import Path\n@functools.cache\ndef read(p):\n    return Path(p).read_text()\na = read(\"/gate.txt\")\nb = read(\"/gate.txt\")\nreturn [a, b, str(read.cache_info())]\n";
        var rerun = await AssertRerunMatchesSync(republish, files);
        Assert.Equal(
            new List<object?> { "G", "G", "CacheInfo(hits=1, misses=1, maxsize=None, currsize=1)" },
            rerun.ReturnValue);
    }

    [Fact]
    public async Task CaughtCancellationStillTerminatesRun()
    {
        // Cancellation is terminal (SPEC 14.3): the token stays cancelled, so execution
        // cannot resume after the handler. FailedCallsDoNotExhaust already proves handler
        // matching itself in async mode.
        const string source = "import functools\nfrom pathlib import Path\n@functools.cache\ndef read(p):\n    return Path(p).read_text()\ntry:\n    read(\"/gate.txt\")\nexcept RuntimeError:\n    pass\nreturn \"continued\"\n";
        var files = new[] { ("/gate.txt", "G") };
        AssertCancelled(await CancelMidReadAsync(source, "/gate.txt", files));
    }

    [Fact]
    public async Task SortWithSuspendingKeyCancelsMidFlight()
    {
        const string source = "from pathlib import Path\nf = open(\"/rows.txt\")\nrows = [line.split() for line in f]\nreturn sorted(rows, key=lambda r: len(Path(\"/gate.txt\").read_text()) + len(r))\n";
        var files = new[] { ("/rows.txt", "b 22\na 1\nc 333\n"), ("/gate.txt", "G") };
        AssertCancelled(await CancelMidReadAsync(source, "/gate.txt", files));
        await AssertRerunMatchesSync(source, files);
    }

    [Fact]
    public async Task HostErrorPairsWithCancellationAndLaterSuccess()
    {
        const string source = "import functools\nfrom pathlib import Path\ndef combine(a, b):\n    return a + int(b) + len(Path(\"/missing.txt\").read_text())\nwith open(\"/data.txt\") as f:\n    return functools.reduce(combine, f, 0)\n";
        var files = new[] { ("/data.txt", Numbers200()) };
        var hostError = await Compile(source).RunAsync(SeedHost(files));
        Assert.False(hostError.Success);
        Assert.Contains("Host", hostError.Failure?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("execution canceled", hostError.Failure?.Message, StringComparison.Ordinal);
        AssertCancelled(await CancelMidReadAsync(source, "/data.txt", files));
        const string healthy = "import functools\nwith open(\"/data.txt\") as f:\n    return functools.reduce(lambda a, b: a + int(b), f, 0)\n";
        var rerun = await AssertRerunMatchesSync(healthy, files);
        Assert.Equal(new BigInteger(19900), rerun.ReturnValue);
    }
}