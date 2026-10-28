using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SubprocessExceptionContractTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeRenderingAwaitsMetadataAndCancellation(bool cancel)
    {
        using var script = new LythonEngine().Compile("""
            import subprocess
            events=[]
            def read(tag):
                with open('/input') as f:
                    events.append(tag)
                    return f.read()
            class Code:
                def __lt__(self,other):read('lt');return False
                def __int__(self):read('int');return 3
            class Command:
                def __str__(self):return read('cmd')
            e=subprocess.CalledProcessError(Code(),Command())
            assert events == []
            assert str(e) == "Command 'value' returned non-zero exit status 3."
            assert events == ['lt','cmd','int']
            e=subprocess.TimeoutExpired(Command(),2)
            assert str(e) == "Command 'value' timed out after 2 seconds"
            print('ok')
            """);
        var host = new DelayedLythonHost(); host.EnableSubprocess(); host.SeedFile("/input", "value");
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var started = cancel ? host.PauseReadUntilCancellation("/input") : null;
        var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
        if (started is not null)
        {
            await started.WaitAsync(cancellation.Token);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
            cancellation.Cancel();
        }
        using (var result = await pending)
        {
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : "ok\n", result.StandardOutput);
            if (cancel) Assert.Contains("canceled", result.Failure?.Message);
            else Assert.True(host.CompletedAsynchronously > 0);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task MetadataIsNotRenderedUntilRequestedAndRenderingRefusalIsTerminal(bool asynchronous, bool render)
    {
        var host = new MockLythonHost(); host.EnableSubprocess();
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile("""
            import subprocess
            subprocess.TimeoutExpired('cmd',1)
            value='x'*262144
            print('gate',end='')
            e=subprocess.TimeoutExpired('cmd',value)
            """ + (render ? "\ntry:str(e)\nexcept MemoryError:print('suppressed')\nprint('after')"
                : "\nassert e.timeout is value\nprint('constructed',end='')"), budget);
        ILythonMemoryReservation? hold = null;
        long started = 0, allocated = 0;
        int thread = 0;
        host.OnStandardOutputWrite = () =>
        {
            var text = host.CapturedStandardOutput();
            if (text == "gate" && hold is null)
            {
                Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes - 16384, out hold));
                thread = Environment.CurrentManagedThreadId;
                started = GC.GetAllocatedBytesForCurrentThread();
            }
            if (text == "gateconstructed" && allocated == 0)
            {
                Assert.Equal(thread, Environment.CurrentManagedThreadId);
                allocated = GC.GetAllocatedBytesForCurrentThread() - started;
            }
        };
        var baseline = budget.ReservedBytes;
        try
        {
            using var result = asynchronous ? await script.RunAsync(host) : script.Run(host);
            Assert.NotNull(hold);
            if (render)
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal("gate", result.StandardOutput);
            }
            else
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("gateconstructed", result.StandardOutput);
#if !DEBUG
                Assert.InRange(allocated, 1, 65536);
#endif
            }
        }
        finally { hold?.Dispose(); }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArgumentsAttributesAliasesAndMessagesFollowPython(bool asynchronous)
    {
        var host = new MockLythonHost();
        host.EnableSubprocess();
        var budget = new LythonMemoryBudget();
        using var script = new LythonEngine().Compile("""
            import subprocess
            for cls,first,second,field in [(subprocess.CalledProcessError,3,'old','returncode'),
                                           (subprocess.TimeoutExpired,'old',1,'timeout')]:
                e=cls(first,second,b'out',b'err')
                args=e.args
                assert args == (first,second,b'out',b'err')
                assert sorted(e.__dict__) == sorted(['cmd',field,'output','stderr'])
                e.stdout=b'new'
                assert e.output is e.stdout
                e.output=b'last'
                assert e.stdout is e.output
                e.__dict__['stdout']='shadow'
                assert e.stdout == b'last'
                e.__dict__={'cmd':'new',field:4,'output':b'x','stderr':None}
                assert e.stdout == b'x' and e.args is args
                del e.output
                try:e.stdout
                except AttributeError:pass
                else:raise AssertionError('missing output')
            e=subprocess.CalledProcessError(returncode=3,cmd='old')
            assert e.args == ()
            assert str(e) == "Command 'old' returned non-zero exit status 3."
            e.cmd='new';e.returncode=4
            assert str(e) == "Command 'new' returned non-zero exit status 4."
            assert repr(e) == 'CalledProcessError()'
            assert subprocess.CalledProcessError(False,'cmd').args == (False,'cmd')
            class Metadata:pass
            value=Metadata()
            assert subprocess.CalledProcessError(value,'cmd').returncode is value
            e=subprocess.TimeoutExpired(cmd='old',timeout=1)
            assert e.args == () and str(e) == "Command 'old' timed out after 1 seconds"
            e.cmd='new';e.timeout=2
            assert str(e) == "Command 'new' timed out after 2 seconds"
            assert repr(e) == 'TimeoutExpired()'
            p=subprocess.CompletedProcess('cmd',3,b'out',b'err')
            try:p.check_returncode()
            except subprocess.CalledProcessError as e:assert e.args == (3,'cmd',b'out',b'err')
            print('ok')
            """, budget);
        var baseline = budget.ReservedBytes;
        for (var i = 0; i < 2; i++)
        {
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("ok\n", result.StandardOutput);
                Assert.True(budget.ReservedBytes > baseline);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
