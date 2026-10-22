using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FunctoolsComparisonKeyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ComparatorResultsUseTheirRequestedRawComparison(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from functools import cmp_to_key
            class Result:
                def __lt__(self,other):return 'lt'
                def __le__(self,other):return 'le'
                def __gt__(self,other):return 'gt'
                def __ge__(self,other):return 'ge'
                def __eq__(self,other):return 'eq'
                def __ne__(self,other):return 'ne'
            key=cmp_to_key(lambda a,b:Result())
            a,b=key(1),key(2)
            print(a<b,a<=b,a>b,a>=b,a==b,a!=b)
            print(a in [b])
            print(sorted([3,1,2],key=cmp_to_key(lambda a,b:(a-b)/2)))
            print(sorted([3,1,2],key=cmp_to_key(lambda a,b:float('nan'))))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("lt le gt ge eq ne\nTrue\n[1, 2, 3]\n[3, 1, 2]\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DifferentFactoriesUseTheLeftComparator(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from functools import cmp_to_key
            a=cmp_to_key(lambda a,b:-0.5)(1)
            b=cmp_to_key(lambda a,b:0.5)(2)
            print(a<b,b<a)
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True False\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("comparer")]
    [InlineData("ordering")]
    [InlineData("truth")]
    [InlineData("equality")]
    public async Task ComparatorResultAndTruthCallbacksAwaitIoAndUnwind(string stage)
    {
        var comparer = stage == "comparer" ? "read();return a-b" : "return Result(a-b)";
        var less = stage == "ordering" ? "read();return self.value<other"
            : stage == "truth" ? "return Flag(self.value<other)" : "return self.value<other";
        var expression = stage == "equality" ? "key(1)==key(1)" : "sorted([3,1,2],key=key)";
        var source = """
            from functools import cmp_to_key
            def read():
                with open('/compare') as f:f.read()
            class Flag:
                def __init__(self,value):self.value=value
                def __bool__(self):
                    read()
                    return self.value
            class Result:
                def __init__(self,value):self.value=value
                def __eq__(self,other):
                    read()
                    return self.value==other
            """ + "\n    def __lt__(self,other):" + less + "\ndef compare(a,b):" + comparer
            + "\nkey=cmp_to_key(compare)\nreturn repr(" + expression + ")";
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/compare", "data");
            var entered = cancel ? host.PauseReadUntilCancellation("/compare") : null;
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
                    Assert.Equal(stage == "equality" ? "True" : "[1, 2, 3]", result.ReturnValue);
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
