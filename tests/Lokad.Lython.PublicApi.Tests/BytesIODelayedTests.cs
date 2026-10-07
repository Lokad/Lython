using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BytesIODelayedTests
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
                yield b'first:'
                with open('/line') as marker:
                    yield marker.read().encode('utf-8')
            s=io.BytesIO(b'a\xffbc')
            print(s.read(Index()),s.tell())
            s.seek(Index())
            print(s.truncate(Index()),s.getvalue())
            s.writelines(lines())
            print(s.getvalue())
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
        Assert.Equal("b'a\\xff' 2\n2 b'a\\xff'\nb'a\\xfffirst:\\xc3\\xa9\\xf0\\x9f\\x98\\x80'\n", sync.StandardOutput);
        Assert.Equal(sync.StandardOutput, asyncResult.StandardOutput);
        Assert.True(host.CompletedAsynchronously >= 4);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationInterruptsTheAwaitedIndexOrLineSource(bool index)
    {
        var source = index ? """
            import io
            class Index:
                def __index__(self):
                    with open('/marker') as marker:
                        return int(marker.read())
            s=io.BytesIO(b'abc')
            s.read(Index())
            print('completed')
            """ : """
            import io
            s=io.BytesIO()
            def lines():
                yield b'first'
                print(s.getvalue())
                with open('/marker') as marker:
                    yield marker.read().encode()
                print('later')
            s.writelines(lines())
            print('completed')
            """;
        var script = Compile(source);
        var host = new DelayedLythonHost();
        host.SeedFile("/marker", "2");
        var started = host.PauseReadUntilCancellation("/marker");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        var result = await pending;
        Assert.False(result.Success);
        Assert.Contains("canceled", result.Failure?.Message);
        Assert.Equal(index ? "" : "b'first'\n", result.StandardOutput);
    }

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return script;
    }
}
