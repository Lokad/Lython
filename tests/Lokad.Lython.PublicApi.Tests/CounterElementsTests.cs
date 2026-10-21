using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CounterElementsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CountsAreReadLazilyAndFailuresLeaveTheIteratorUsable(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from collections import Counter
            c=Counter(a=1,b=1.5,c=1)
            e=c.elements()
            c['a']=2
            print(iter(e) is e)
            print(next(e),next(e))
            try: next(e)
            except TypeError: print('bad count')
            print(next(e))
            print(list(e))
            class Count:
                def __index__(self):
                    print('index')
                    return 2
            c=Counter()
            c['x']=Count()
            e=c.elements()
            print('created')
            print(list(e))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True\na a\nbad count\nc\n[]\ncreated\nindex\n['x', 'x']\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirstPullDoesNotMaterializeTheRepetitions(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("from collections import Counter\ne=Counter(a=2**60).elements()\nreturn [next(e),next(e)]");
        var budget = new LythonMemoryBudget(32768);
        for (var pass = 0; pass < 2; pass++)
        {
            using (var result = asynchronous
                ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new List<object?> { "a", "a" }, result.ReturnValue);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CountIndexCanSuspendAndCancellationAwaitsItsCleanup()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("""
            from collections import Counter
            class Count:
                def __index__(self):
                    with open('/count') as f: f.read()
                    return 2
            c=Counter()
            c['a']=Count()
            return list(c.elements())
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/count", "data");
            var entered = cancel ? host.PauseReadUntilCancellation("/count") : null;
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
                    Assert.Equal(new List<object?> { "a", "a" }, result.ReturnValue);
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
