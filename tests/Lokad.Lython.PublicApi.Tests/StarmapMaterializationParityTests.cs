using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N09: materialization results carry an explicit reservation lifetime.
// Starmap sync/async share admission and cancellation; the async element
// list stays leased beside the argument array and callback.
public sealed class StarmapMaterializationParityTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MemoryBudget = new LythonMemoryBudget(8192),
    };

    [Fact]
    public async Task StarmapArgumentsRefuseUnderSharedAllowanceInBothModes()
    {
        const string code = "import itertools\nreturn next(itertools.starmap(lambda *x: len(x), [range(1000)]))\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(code);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost(), Tiny());
        Assert.False(sync.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, sync.FailureAttribution?.Reason);
        var asyncResult = await script.RunAsync(new MockLythonHost(), Tiny());
        Assert.False(asyncResult.Success);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, asyncResult.FailureAttribution?.Reason);
    }

    [Fact]
    public async Task StarmapFunded_MatchesBothModes()
    {
        const string code = "import itertools\nreturn list(itertools.starmap(lambda a, b: a + b, [(1, 2), (3, 4)]))\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task StarmapDelayedSource_ComposesAsyncAndFailsFastSync()
    {
        const string code = "import itertools\nwith open(\"/r.txt\") as f:\n    return list(itertools.starmap(lambda a: a.strip(), [(line,) for line in f]))\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(code);
        Assert.True(script.IsValid);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/r.txt", "a\nb\n");
        var asyncResult = await script.RunAsync(delayed);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.True(delayed.CompletedAsynchronously > 0);
        var syncHost = new DelayedLythonHost();
        syncHost.SeedFile("/r.txt", "a\nb\n");
        var sync = script.Run(syncHost);
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
    }

    [Fact]
    public async Task StarmapNestedCallback_SucceedsFunded()
    {
        const string code = "import itertools\nreturn list(itertools.starmap(lambda *a: list(a), [[1, 2], [3]]))\n";
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactRowsKeepLiveDefaultsAndRetainedAliases(bool asynchronous)
    {
        const string source = """
            from itertools import starmap
            class Policy:
                def apply(self, value, /, offset=1, *, scale=2):
                    return (value + offset) * scale
                def identity(self, value):
                    return value
            policy = Policy()
            saved = policy.apply
            Policy.apply = lambda self, value: -1
            print(list(starmap(saved, [(1,), (2,)])), list(map(saved, [1, 2])))
            saved.__func__.__defaults__ = (5,)
            saved.__func__.__kwdefaults__ = {'scale': 3}
            print(list(starmap(saved, [(1,), (2,)])), list(map(saved, [1, 2])))
            def make_row(value):
                return (value + 1,)
            row = make_row(1000)
            print(next(starmap(policy.identity, [row])) is row[0])
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), options)
            : script.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[4, 6] [4, 6]\n[18, 21] [18, 21]\nTrue\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task ExactTupleCallbackKeepsBorrowUntilSuspendedCallFinishes()
    {
        const string source = """
            from itertools import starmap
            def use(value):
                with open('pause') as stream:
                    stream.read()
                return value
            marker = []
            row = (marker,)
            print(next(starmap(use, [row])) is marker)
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("\n", script.Diagnostics));
        var budget = new LythonMemoryBudget(1024 * 1024);
        var blocked = new DelayedLythonHost();
        blocked.SeedFile("/pause", "content");
        var started = blocked.PauseReadUntilCancellation("/pause");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(blocked,
            new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        try
        {
            await started.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var interrupted = await pending;
            Assert.Equal(LythonFailureReason.Cancelled, interrupted.FailureAttribution?.Reason);
            Assert.Empty(interrupted.StandardOutput);
        }
        Assert.Equal(1, blocked.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);

        var host = new DelayedLythonHost();
        host.SeedFile("/pause", "content");
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True\n", result.StandardOutput);
        }
        Assert.Equal(1, host.ClosedFileCount);
        Assert.Equal(0, budget.ReservedBytes);
    }
}
