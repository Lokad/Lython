using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CounterRankingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LimitsAndGuestComparisonsFollowPythonEvaluationPoints(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from collections import Counter
            c=Counter(a=object(),b=object())
            print(c.most_common(0),c.most_common(-1))
            class Value:
                def __init__(self,n): self.n=n
                def __lt__(self,other): print('lt'); return self.n<other.n
                def __repr__(self): return 'V'+str(self.n)
            class Limit:
                def __eq__(self,other): print('eq'); return False
                def __ge__(self,other): print('ge'); return True
                def __index__(self): print('index'); return 1
            c=Counter(a=Value(1),b=Value(2))
            print(c.most_common(Limit()))
            print(c.most_common(1))
            print(c)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[] []\neq\nge\nlt\nindex\n[('b', V2)]\nlt\n[('b', V2)]\nlt\nCounter({'b': V2, 'a': V1})\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReprFallbackObservesCurrentItemsAndDoesNotRetryValueRepr(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from collections import Counter
            c=Counter()
            class Value:
                def __lt__(self,other):
                    c['new']=7
                    raise TypeError()
                def __repr__(self): return 'V'
            c['a']=Value()
            c['b']=Value()
            print(c)
            class BadRepr:
                def __repr__(self): print('repr'); raise TypeError()
            try: repr(Counter(a=BadRepr()))
            except TypeError: print('caught')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("Counter({'a': V, 'b': V, 'new': 7})\nrepr\ncaught\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("c.most_common()", "/compare")]
    [InlineData("c.most_common(1)", "/compare")]
    [InlineData("c.most_common(Equal())", "/equal")]
    [InlineData("c.most_common(Bound())", "/index")]
    [InlineData("c.most_common(Negative())", "/negative")]
    public async Task RankingAndLimitHooksSuspendAndCancellationUnwindsScratch(string expression, string marker)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("""
            from collections import Counter
            class Value:
                def __init__(self,n): self.n=n
                def __lt__(self,other):
                    with open('/compare') as f: f.read()
                    return self.n<other.n
            class Equal:
                def __eq__(self,other):
                    with open('/equal') as f: f.read()
                    return True
            class Bound:
                def __eq__(self,other): return False
                def __ge__(self,other): return True
                def __index__(self):
                    with open('/index') as f: f.read()
                    return 1
            class Negative:
                def __eq__(self,other): return False
                def __ge__(self,other): return False
                def __neg__(self):
                    with open('/negative') as f: f.read()
                    return 0
            c=Counter(a=Value(1),b=Value(2))
            """ + "\nreturn len(" + expression + ")", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            foreach (var path in new[] { "/compare", "/equal", "/index", "/negative" }) host.SeedFile(path, "data");
            var entered = cancel ? host.PauseReadUntilCancellation(marker) : null;
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                if (entered is not null)
                {
                    Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
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
