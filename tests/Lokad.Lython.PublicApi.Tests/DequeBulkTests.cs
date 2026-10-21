using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DequeBulkTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepetitionUsesTheRetainedSuffixAndPreservesAliases(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from collections import deque
            d=deque([1,2],maxlen=3)
            print(list(d*2147483648))
            alias=d
            d*=2147483648
            print(alias is d,list(d))
            d=deque([1,2],maxlen=5)
            print(list(d*4))
            d*=4
            print(list(d))
            d*=1
            print(list(d))
            d*=0
            print(list(d))
            print(list(deque()*2147483648))
            for n in [2**63,-2**63-1]:
                try: deque()*n
                except OverflowError: print('overflow')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[2, 1, 2]\nTrue [2, 1, 2]\n[2, 1, 2, 1, 2]\n[2, 1, 2, 1, 2]\n[2, 1, 2, 1, 2]\n[]\n[]\noverflow\noverflow\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InplaceExtensionPublishesEachYieldAndSnapshotsOnlySelf(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from collections import deque
            d=deque([0],maxlen=2)
            def values():
                for i in range(3):
                    print(list(d))
                    yield i+1
            d+=values()
            print(list(d))
            d=deque([1,2],maxlen=3)
            d+=d
            print(list(d))
            d=deque(maxlen=2)
            d+=range(20000)
            return list(d)
            """);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("[0]\n[0, 1]\n[1, 2]\n[2, 3]\n[2, 1, 2]\n", result.StandardOutput);
            Assert.Equal(new List<object?> { new BigInteger(19998), new BigInteger(19999) }, result.ReturnValue);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlicesRejectBeforeConvertingBoundsOrDrainingReplacement(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from collections import deque
            class Bound:
                def __index__(self): print('bound'); return 0
            def replacement():
                print('replacement')
                yield 9
            d=deque([1,2,3])
            try: d[Bound():]
            except TypeError: print('read')
            try: d[Bound():]=replacement()
            except TypeError: print('write')
            try: del d[Bound():]
            except TypeError: print('delete')
            d[-1]=4
            print(d[0],d[-1],list(d))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("read\nwrite\ndelete\n1 4 [1, 2, 4]\n", result.StandardOutput);
    }

    [Fact]
    public async Task InplaceExtensionAwaitsInputAndCancellationUnwindsIt()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("""
            from collections import deque
            d=deque(maxlen=2)
            def values():
                with open('/rows') as rows:
                    for row in rows: yield int(row)
            d+=values()
            return list(d)
            """, budget);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/rows", "1\n2\n3\n");
            var entered = cancel ? host.PauseReadUntilCancellation("/rows") : null;
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
                    Assert.Equal(new List<object?> { new BigInteger(2), new BigInteger(3) }, result.ReturnValue);
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
