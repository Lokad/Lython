using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N09: materialization results carry an explicit reservation lifetime.
// Starmap sync/async share count/work checks (mode parity); the async element
// list stays leased beside the argument array and callback.
public sealed class StarmapMaterializationParityTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MaxExecutionMemoryBytes = 1048576,
        MaxExecutionSteps = 100000,
        MaxCollectionSize = 10,
    };

    [Fact]
    public async Task StarmapDivergenceCase_RejectsInBothModes()
    {
        const string code = "import itertools\nreturn next(itertools.starmap(lambda *x: len(x), [range(1000)]))\n";
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost(), Tiny());
        Assert.False(sync.Success);
        Assert.True(sync.Failure?.ExceptionType is "MemoryError" or "RuntimeError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(new MockLythonHost(), Tiny());
        Assert.False(asyncResult.Success);
        Assert.True(asyncResult.Failure?.ExceptionType is "MemoryError" or "RuntimeError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task StarmapFunded_MatchesBothModes()
    {
        const string code = "import itertools\nreturn list(itertools.starmap(lambda a, b: a + b, [(1, 2), (3, 4)]))\n";
        var script = new LythonEngine().Compile(code);
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
        var script = new LythonEngine().Compile(code);
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
        var script = new LythonEngine().Compile(code);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }
}
