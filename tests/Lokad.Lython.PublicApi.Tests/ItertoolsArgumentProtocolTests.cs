using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ItertoolsArgumentProtocolTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConversionAndNegativeChecksPreserveConstructorSpecificEffects(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from itertools import combinations,combinations_with_replacement,permutations
            class N:
                def __index__(self):
                    print('index')
                    return 2
            def source():
                print('pool')
                yield 1
                yield 2
            print(list(combinations(source(),N())))
            print(list(combinations_with_replacement(source(),N())))
            try:combinations(source(),'bad')
            except TypeError:print('type')
            try:combinations(source(),-1)
            except ValueError:print('negative')
            try:permutations(source(),N())
            except TypeError:print('native r')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("index\npool\n[(1, 2)]\nindex\npool\n[(1, 1), (1, 2), (2, 2)]\ntype\npool\nnegative\npool\nnative r\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StrictTruthPrecedesRangeErrorsAndHugeBatchesStayBounded(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from itertools import batched,tee
            class Flag:
                def __bool__(self):
                    print('bool')
                    return False
            print(list(batched([1,2,3],2,strict=Flag())))
            try:batched([1],0,strict=Flag())
            except ValueError:print('size')
            print(list(batched([1,2],2**40)))
            print(tee(object(),0))
            """);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("bool\n[(1, 2), (3,)]\nbool\nsize\n[(1, 2)]\n()\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("list(repeat('x',Count()))", "['x', 'x']")]
    [InlineData("list(product([1,2],repeat=Count()))", "[(1, 1), (1, 2), (2, 1), (2, 2)]")]
    [InlineData("list(combinations([1,2,3],Count()))", "[(1, 2), (1, 3), (2, 3)]")]
    [InlineData("list(combinations_with_replacement([1,2],Count()))", "[(1, 1), (1, 2), (2, 2)]")]
    [InlineData("[list(x) for x in tee([1,2],Count())]", "[[1, 2], [1, 2]]")]
    [InlineData("list(batched([1,2,3],Count()))", "[(1, 2), (3,)]")]
    [InlineData("list(batched([1,2,3],2,strict=Flag()))", "[(1, 2), (3,)]")]
    public async Task FactoriesAwaitActualIndexOrTruthIoAndUnwindCancellation(string expression, string expected)
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("""
            from itertools import repeat,product,combinations,combinations_with_replacement,tee,batched
            class Count:
                def __index__(self):
                    with open('/n') as f:return int(f.read())
            class Flag:
                def __bool__(self):
                    with open('/n') as f:return f.read()=='yes'
            return repr(
            """ + expression + ")", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/n", "2");
            var entered = cancel ? host.PauseReadUntilCancellation("/n") : null;
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
                    Assert.Equal(expected, result.ReturnValue);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedIndexHookCannotResumeGuestWork(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from itertools import product
            class N:
                def __index__(self):
                    try:x=[None]*100000
                    except MemoryError:pass
                    print('continued')
                    return 2
            print(list(product([1],repeat=N())))
            """);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous
            ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
            : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
