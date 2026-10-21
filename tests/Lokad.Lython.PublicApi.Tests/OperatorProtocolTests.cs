using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OperatorProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LengthHintFallbackCannotSuppressRefusedAdmission(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import operator
            class Value:
                def __length_hint__(self):
                    try: bytes(200000)
                    finally: raise TypeError('replace refusal')
            try: operator.length_hint(Value(),4)
            except Exception: print('unreachable handler')
            print('unreachable continuation')
            """);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HelpersUseTheSameProtocolsAsExpressions(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import operator
            class F:
                def __bool__(self): print('bool'); return False
            print(operator.truth(F()),operator.not_(F()))
            class E:
                def __eq__(self,other): print('eq'); return 'answer'
                def __lt__(self,other): print('lt'); return 'less'
            a=E(); b=E()
            print(operator.eq(a,b),operator.lt(a,b))
            print(operator.countOf([a],b),operator.indexOf([a],b))
            class C:
                def __contains__(self,item): print('contains'); return True
            print(operator.contains(C(),1))
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("bool\nbool\nFalse True\neq\nlt\nanswer less\neq\neq\n1 0\ncontains\nTrue\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LengthHintValidatesDefaultsAndPreservesLookupAndCallErrors(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import operator
            try: operator.length_hint([], 'bad')
            except TypeError: print('default')
            print(operator.length_hint(object(),-1))
            values=iter([1,2])
            print(operator.length_hint(values,-1),values.__length_hint__())
            next(values)
            print(operator.length_hint(values,-1))
            list(values)
            print(operator.length_hint(values,-1),operator.length_hint(iter([]),-1))
            class CallBad:
                def __length_hint__(self): raise TypeError('call')
            class LookupBad:
                @property
                def __length_hint__(self): raise TypeError('lookup')
            print(operator.length_hint(CallBad(),-1))
            try: operator.length_hint(LookupBad(),-1)
            except TypeError: print('lookup')
            class Declined:
                def __length_hint__(self): return NotImplemented
            print(operator.length_hint(Declined(),-1))
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("default\n-1\n2 2\n1\n0 0\n-1\nlookup\n-1\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("operator.truth(Value())")]
    [InlineData("operator.not_(Value())")]
    [InlineData("operator.eq(Value(),object())")]
    [InlineData("operator.lt(Value(),object())")]
    [InlineData("operator.contains(Value(),1)")]
    [InlineData("operator.countOf([Value()],object())")]
    [InlineData("operator.indexOf([Value()],object())")]
    [InlineData("operator.length_hint(Value())")]
    public async Task ProtocolsSuspendAndCancellationUnwindsTheirOwners(string expression)
    {
        var source = """
            import operator
            class Value:
                def pause(self):
                    with open('/marker') as marker: marker.read()
                def __bool__(self): self.pause(); return True
                def __eq__(self,other): self.pause(); return True
                def __lt__(self,other): self.pause(); return True
                def __contains__(self,item): self.pause(); return True
                def __length_hint__(self): self.pause(); return 1
            """ + "\nreturn " + expression;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/marker", "data");
            var entered = cancel ? host.PauseReadUntilCancellation("/marker") : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
                    Assert.Same(entered, first);
                    Assert.False(pending.IsCompleted);
                    Assert.True(budget.ReservedBytes > baseline);
                    cancellation.Cancel();
                }
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                if (cancel) Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                else
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.True(host.CompletedAsynchronously > 0);
                }
            }
            finally
            {
                cancellation.Cancel();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}
