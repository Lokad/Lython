using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringIODelayedTests
{
    [Fact]
    public async Task IndexSlotsAndWritelinesAwaitTheirGuestEffects()
    {
        var script = Compile("""
            import io
            class Index:
                def __index__(self):
                    with open('/size') as marker:
                        return int(marker.read())
            def lines():
                yield 'first:'
                with open('/line') as marker:
                    yield marker.read()
            s=io.StringIO('a😀bc')
            print(repr(s.read(Index())), s.tell())
            s.seek(Index())
            print(s.truncate(Index()), repr(s.getvalue()))
            s.writelines(lines())
            print(repr(s.getvalue()))
            """);
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/size", "2");
        syncHost.SeedFile("/line", "é😀");
        var sync = script.Run(syncHost);
        var host = new DelayedLythonHost();
        host.SeedFile("/size", "2");
        host.SeedFile("/line", "é😀");
        var asyncResult = await script.RunAsync(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("'a😀' 2\n2 'a😀'\n'a😀first:é😀'\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, asyncResult.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 4);
    }

    [Fact]
    public async Task CancellationInterruptsTheAwaitedLineSourceBeforeLaterPulls()
    {
        var script = Compile("""
            import io
            s=io.StringIO()
            def lines():
                yield 'first'
                print(repr(s.getvalue()))
                with open('/line') as marker:
                    yield marker.read()
                print('later')
            s.writelines(lines())
            print('completed')
            """);
        var host = new DelayedLythonHost();
        host.SeedFile("/line", "second");
        var started = host.PauseReadUntilCancellation("/line");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal("'first'\n", result.StandardOutput);
    }

    [Fact]
    public async Task LargeSeekIsCheapAndDeniedWritesKeepThePrefix()
    {
        var script = Compile("""
            import io
            s=io.StringIO('kept')
            s.seek(10**12)
            print(s.write(''), s.tell())
            try:
                s.write('x')
            except MemoryError:
                print(repr(s.getvalue()), s.tell())
            s.seek(0)
            s.write('yes')
            print(repr(s.getvalue()))
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        foreach (var result in new[] { script.Run(new MockLythonHost(), options), await script.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("0 1000000000000\n'kept' 1000000000000\n'yest'\n", result.StandardOutput);
        }
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
