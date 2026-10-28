using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ShlexStreamContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StreamsHaveOneCursorAndConstructionDoesNotConsumeInput(bool asynchronous)
    {
        var host = new MockLythonHost(); host.SeedFile("/input", "alpha beta\nomega");
        using var script = new LythonEngine().Compile("""
            import shlex,io
            f=open('/input')
            l=shlex.shlex(f,posix=True)
            assert f.tell() == 0
            assert l.instream is f and l.get_token() == 'alpha' and f.tell() == 6
            assert f.read() == 'beta\nomega'
            assert l.get_token() is None
            l=shlex.shlex('a b',posix=True)
            assert isinstance(l.instream,io.StringIO)
            assert l.instream.read(1) == 'a' and l.get_token() == 'b'
            f=io.StringIO('a😀 b')
            l=shlex.shlex(f,posix=True);l.whitespace_split=True
            assert f.tell() == 0 and l.get_token() == 'a😀' and f.tell() == 3
            l.push_source('c d')
            assert list(l) == ['c','d','b']
            assert l.instream is f and not f.closed
            source='a😀 b'
            s=shlex.shlex(source,posix=True).instream
            assert s.read(2) == 'a😀'
            s.seek(0);s.truncate(2)
            assert s.getvalue() == 'a😀'
            s.seek(1);s.write('X')
            assert s.getvalue() == 'aX' and source == 'a😀 b'
            s.close()
            l=shlex.shlex('a\nb',posix=True);l.lineno=2147483647
            assert l.get_token() == 'a' and l.lineno == 2147483648
            l=shlex.shlex('#comment\na',posix=True);l.lineno=2147483647
            assert l.get_token() == 'a' and l.lineno == 2147483648
            print('ok')
            """);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
                   : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("ok\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task EqualityTruthAndInputReadsReallySuspend()
    {
        using var script = new LythonEngine().Compile("""
            import shlex
            events=[]
            class Truth:
                def __bool__(self):
                    with open('/input') as f:events.append('bool:'+f.read(1))
                    return False
            class End:
                def __eq__(self,other):
                    with open('/input') as f:events.append('eq:'+f.read(1))
                    return Truth()
            l=shlex.shlex('abc',posix=True);l.eof=End()
            assert next(l) == 'abc'
            assert events == ['eq:a','bool:a','eq:a','bool:a']
            events.clear()
            token=End();l.eof=token;l.push_token(token)
            assert next(l) is token
            assert events == ['eq:a','bool:a']
            with open('/input') as f:
                l=shlex.shlex(f,posix=True)
                assert f.tell() == 0 and l.get_token() == 'abc'
            print('ok')
            """);
        var host = new DelayedLythonHost(); host.SeedFile("/input", "abc def");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var result = await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("ok\n", result.StandardOutput);
            Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CancellationDuringInputUnwindsBeforeAnotherToken()
    {
        using var script = new LythonEngine().Compile("""
            import shlex
            with open('/input') as f:
                l=shlex.shlex(f,posix=True)
                print('constructed')
                l.get_token()
                print('after')
            """);
        var host = new DelayedLythonHost(); host.SeedFile("/input", "abc");
        var started = host.PauseReadUntilCancellation("/input");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        await started.WaitAsync(cancellation.Token);
        Assert.False(pending.IsCompleted);
        Assert.True(budget.ReservedBytes > 0);
        cancellation.Cancel();
        using (var result = await pending)
        {
            Assert.False(result.Success);
            Assert.Equal("constructed\n", result.StandardOutput);
            Assert.Contains("canceled", result.Failure?.Message);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
