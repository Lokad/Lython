using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class TotalOrderingProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DefaultEqualityAndInheritedOrderingArePreserved(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from functools import total_ordering
            @total_ordering
            class V:
                def __init__(self,n):self.n=n
                def __lt__(self,other):return self.n<other.n
            print(V(1)<=V(2))
            class Base:
                def __gt__(self,other):return 'base'
            @total_ordering
            class W(Base):
                def __lt__(self,other):return True
            print(W()>W())
            class Root:
                def __lt__(self,other):return True
            @total_ordering
            class Child(Root):pass
            print(Child()<=Child())
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True\nbase\nTrue\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootsKeepTheirReceiverAndRawShortCircuitResults(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from functools import total_ordering
            @total_ordering
            class V:
                def __init__(self,n):self.n=n
                def __lt__(self,other):
                    print('lt',self.n,other.n)
                    return self.n<other.n
                def __eq__(self,other):raise RuntimeError('unexpected equality')
            print(V(1)>V(2))
            class Flag:
                def __bool__(self):
                    print('truth')
                    return False
            flag=Flag()
            @total_ordering
            class W:
                def __le__(self,other):return flag
                def __eq__(self,other):raise RuntimeError('unexpected equality')
            print(W().__lt__(W()) is flag)
            @total_ordering
            class N:
                def __lt__(self,other):return NotImplemented
                def __eq__(self,other):raise RuntimeError('unexpected equality')
            print(N().__le__(object()) is NotImplemented)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("lt 1 2\nFalse\ntruth\nTrue\nTrue\n", result.StandardOutput);
    }

    public static IEnumerable<object[]> Formulas()
    {
        var cases = new (string Root, string Result, string Expected)[]
        {
            ("__lt__", "True", "[True, True, False, False]"),
            ("__lt__", "False", "[False, 'eq', 'ne', True]"),
            ("__le__", "True", "['ne', True, False, 'eq']"),
            ("__le__", "False", "[False, False, True, True]"),
            ("__gt__", "True", "[False, False, True, True]"),
            ("__gt__", "False", "['ne', True, False, 'eq']"),
            ("__ge__", "True", "[False, 'eq', 'ne', True]"),
            ("__ge__", "False", "[True, True, False, False]")
        };
        foreach (var item in cases)
            foreach (var asynchronous in new[] { false, true })
                yield return [item.Root, item.Result, item.Expected, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Formulas))]
    public async Task PythonRootFormulasPreserveRawEqualityResults(string root, string answer, string expected, bool asynchronous)
    {
        using var script = new LythonEngine().Compile($$"""
            from functools import total_ordering
            @total_ordering
            class V:
                def {{root}}(self,other):return {{answer}}
                def __eq__(self,other):return 'eq'
                def __ne__(self,other):return 'ne'
            a,b=V(),V()
            print([a<b,a<=b,a>b,a>=b])
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(expected + "\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("def __lt__(self,other):return read()=='yes'", "a>b")]
    [InlineData("def __lt__(self,other):return Flag()\n def __eq__(self,other):return True", "a<=b")]
    [InlineData("def __lt__(self,other):return False\n def __eq__(self,other):return read()=='no'", "a<=b")]
    [InlineData("def __lt__(self,other):return False\n def __ne__(self,other):return read()=='no'", "a>b")]
    [InlineData("__lt__=Descriptor()", "a>b")]
    public async Task RootTruthEqualityAndClassDescriptorCallbacksAwaitIo(string body, string expression)
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("""
            from functools import total_ordering
            def read():
                with open('/order') as f:return f.read()
            def root(self,other):return False
            class Descriptor:
                def __get__(self,instance,owner):
                    read()
                    return root
            class Flag:
                def __bool__(self):return read()=='yes'
            @total_ordering
            class V:
            """ + "\n " + body + "\na,b=V(),V()\nreturn " + expression, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/order", "no");
            var entered = cancel ? host.PauseReadUntilCancellation("/order") : null;
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
                    Assert.Equal(true, result.ReturnValue);
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
