using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeReceiverPipelineTests
{
    public static IEnumerable<object[]> Controls()
    {
        yield return ["append-alias-and-bound-value", """
            def add(out, value):
                out.append(value)
                return out[-1]
            rows=[]
            value=10**50
            assert add(rows,value) is value
            saved=rows.append
            saved(value)
            assert rows[0] is rows[1]
            def observed(out,n):
                value=n+1000
                out.append(value)
                out.append(value)
                return value is out[-1] and out[-1] is out[-2]
            print(observed(rows,7),len(rows))
            """, "True 4\n"];
        yield return ["evaluated-receiver-before-argument-rebinding", """
            first=[]
            second=[]
            target=first
            def change():
                global target
                target=second
                return 7
            def forward(): target.append(change())
            forward()
            print(first,second,target is second)
            """, "[7] [] True\n"];
        yield return ["closure-and-recursive-handoff", """
            def outer():
                out=[]
                def visit(n):
                    out.append(n)
                    if n: visit(n-1)
                visit(20)
                return out
            print(outer()==list(range(20,-1,-1)))
            """, "True\n"];
        yield return ["late-child-fault-after-native-effects", """
            def visit(out,n):
                out.append(n)
                if n==0: return 1//0
                return visit(out,n-1)
            out=[]
            try: visit(out,12)
            except ZeroDivisionError: pass
            print(out==list(range(12,-1,-1)))
            """, "True\n"];
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public async Task HelpersPreserveReceiverEffectsAndAliases(string name, string source, string expected)
    {
        _ = name;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedAppendPrecedesActualIoAndCleanup(bool cancel)
    {
        const string source = """
            def emit(out,value,sink):
                out.append(value)
                sink.write(str(len(out))*65536)
                return out
            with open('/audit.txt','a') as sink:
                result=emit([],'once',sink)
            return result
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost(); host.SeedFile("/audit.txt", "before");
            var entered = cancel ? host.PauseWriteUntilCancellation("/audit.txt") : null;
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10)));
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(!cancel, result.Success);
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                else Assert.Equal(new object[] { "once" }, Assert.IsAssignableFrom<IReadOnlyList<object>>(result.ReturnValue));
                Assert.Equal(cancel ? "before" : "before" + new string('1', 65536), host.ReadText("/audit.txt"));
                Assert.True(host.CompletedAsynchronously > 0);
            }
            finally
            {
                cancellation.Cancel();
                using var joined = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppendRefusalCannotBeSuppressedOrContinueGuestEffects(bool asynchronous)
    {
        const string source = """
            import time
            out=[None]*8
            def append(out,value):
                try: out.append(value)
                except MemoryError: return 'suppressed'
                finally: return len(out)
            time.time()
            append(out,'value')
            print('must not continue')
            """;
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        var budget = new AdmissionGateBudget();
        var host = new AdmissionGateHost(budget, 64);
        using (var result = asynchronous ? await script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget })
            : script.Run(host, new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }
}
