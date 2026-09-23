using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N31: async contract boundaries. Truthiness dispatches (__bool__/__len__)
// compose through suspension in any()/all()/bool() in both modes; key dispatch
// (dict/set hash/eq, cache key building, index coercion) stays synchronous by
// contract and rejects suspending callbacks explicitly (SPEC 14.3 requires
// awaiting only iteration, materialization and combinators). Operators,
// containment and ordering keys compose; the pins below guard the boundary.
public sealed class AsyncBoundaryContractTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static async Task AssertBothModes(string source, object? expected)
    {
        var script = Compile(source);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    private static DelayedLythonHost SeedDelayedHost()
    {
        var host = new DelayedLythonHost();
        host.SeedFile("/g.txt", "x");
        return host;
    }

    [Fact]
    public async Task AnyAllDispatchDunderProtocols()
    {
        await AssertBothModes(
            """
            class F:
                def __bool__(self):
                    return False
            class T:
                def __bool__(self):
                    return True
            class E:
                def __len__(self):
                    return 0
            class N:
                def __len__(self):
                    return 3
            return [any([F()]), any([T()]), all([T()]), all([F()]), any([E()]), any([N()]), all([N()]), all([E()])]
            """,
            new List<object?> { false, true, true, false, false, true, true, false });
    }

    [Fact]
    public async Task AnyAllSuspendingBoolComposes()
    {
        const string source = """
            from pathlib import Path
            class T:
                def __bool__(self):
                    return len(Path("/g.txt").read_text()) > 0
            return [any([T()]), all([T()])]
            """;
        var script = Compile(source);
        var asyncResult = await script.RunAsync(SeedDelayedHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { true, true }, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task AnySuspendingBoolCancelsMidFlight()
    {
        const string source = """
            from pathlib import Path
            class T:
                def __bool__(self):
                    return len(Path("/g.txt").read_text()) > 0
            return any([T(), T()])
            """;
        var host = SeedDelayedHost();
        using var cancellation = new CancellationTokenSource();
        var readStarted = host.PauseReadUntilCancellation("/g.txt");
        var task = Compile(source).RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await readStarted.WaitAsync(Watchdog);
        cancellation.Cancel();
        var result = await task.WaitAsync(Watchdog);
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("execution canceled", result.Failure?.Message, StringComparison.Ordinal);
        var rerun = await Compile(source).RunAsync(SeedDelayedHost());
        Assert.True(rerun.Success, rerun.Failure?.Message);
        Assert.Equal(true, rerun.ReturnValue);
    }

    [Fact]
    public async Task BoolSuspendingComposes()
    {
        const string source = """
            from pathlib import Path
            class T:
                def __bool__(self):
                    return len(Path("/g.txt").read_text()) > 0
            return bool(T())
            """;
        var asyncResult = await Compile(source).RunAsync(SeedDelayedHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(true, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task EqualitySuspendingComposes()
    {
        const string source = """
            from pathlib import Path
            class K:
                def __hash__(self):
                    return 0
                def __eq__(self, other):
                    return len(Path("/g.txt").read_text()) > 0
            return [K() == K(), K() != K()]
            """;
        var asyncResult = await Compile(source).RunAsync(SeedDelayedHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new List<object?> { true, false }, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ContainmentSuspendingComposes()
    {
        const string source = """
            from pathlib import Path
            class K:
                def __eq__(self, other):
                    return len(Path("/g.txt").read_text()) > 0
            return K() in [K()]
            """;
        var asyncResult = await Compile(source).RunAsync(SeedDelayedHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(true, asyncResult.ReturnValue);
    }

    private static async Task AssertKeyRestriction(string source)
    {
        var result = await Compile(source).RunAsync(SeedDelayedHost());
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("cannot run synchronously", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DictKeyDispatchRejectsSuspension()
    {
        await AssertKeyRestriction("""
            from pathlib import Path
            class K:
                def __hash__(self):
                    return 0
                def __eq__(self, other):
                    return len(Path("/g.txt").read_text()) > 0
            d = {K(): 1}
            return d.pop(K())
            """);
    }

    [Fact]
    public async Task SetKeyDispatchRejectsSuspension()
    {
        await AssertKeyRestriction("""
            from pathlib import Path
            class K:
                def __hash__(self):
                    return len(Path("/g.txt").read_text())
                def __eq__(self, other):
                    return True
            s = set()
            s.add(K())
            return len(s)
            """);
    }

    [Fact]
    public async Task CacheKeyDispatchRejectsSuspension()
    {
        await AssertKeyRestriction("""
            import functools
            from pathlib import Path
            class K:
                def __hash__(self):
                    return len(Path("/g.txt").read_text())
                def __eq__(self, other):
                    return True
            @functools.cache
            def f(k):
                return 7
            return f(K())
            """);
    }
}