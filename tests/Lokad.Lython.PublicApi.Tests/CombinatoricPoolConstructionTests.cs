using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class CombinatoricPoolConstructionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroRepeatDoesNotAcquireOrConsumeItsInputs(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from itertools import product
            def source():
                print('pulled')
                yield 1
            s=source()
            print(list(product(s,object(),repeat=0)))
            print(next(s))
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[()]\npulled\n1\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductTableOverflowIsCatchableAndPrecedesInputEffects(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            from itertools import product
            def source():
                print('pulled')
                yield 1
            try: product(source(),source(),repeat=2**62)
            except OverflowError: print('overflow')
            print('after')
            """);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("overflow\nafter\n", result.StandardOutput);
    }

    [Theory]
    [InlineData("product(source())", "(1,)")]
    [InlineData("combinations(source(),2)", "(1, 2)")]
    [InlineData("combinations_with_replacement(source(),2)", "(1, 1)")]
    [InlineData("permutations(source(),2)", "(1, 2)")]
    public async Task PoolsStayFundedAcrossRealInputSuspensionAndCleanup(string expression, string expected)
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("""
            from itertools import product,combinations,combinations_with_replacement,permutations
            def source():
                with open('/rows') as f:
                    for c in f.read():yield int(c)
            return repr(next(
            """ + expression + "))", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var cancel in new[] { false, true })
        {
            var host = new DelayedLythonHost();
            host.SeedFile("/rows", "12");
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
}
