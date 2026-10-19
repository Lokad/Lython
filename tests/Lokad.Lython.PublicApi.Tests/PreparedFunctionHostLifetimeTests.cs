using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedFunctionHostLifetimeTests
{
    private const string Source = """
        def decorate(fn):
            with open('/decorate') as source:
                source.read()
            return fn
        def make():
            seed = [10]
            @decorate
            def inner(delta=(yield 'default')):
                with open('/body') as source:
                    source.read()
                seed[0] += delta[0]
                return seed[0]
            yield inner
        g = make()
        next(g)
        values = [2]
        function = g.send(values)
        g.close()
        values[0] = 4
        return [function(), values]
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedGeneratorDefaultsAndCellsRemainFundedThroughHostReads(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            ILythonHost host = asynchronous ? Seed(new DelayedLythonHost()) : Seed(new MockLythonHost());
            using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new List<object?> { new BigInteger(14), new List<object?> { new BigInteger(4) } }, result.ReturnValue);
                Assert.True(budget.ReservedBytes > baseline);
                if (host is DelayedLythonHost delayed) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("/decorate")]
    [InlineData("/body")]
    public async Task CancellationDuringPreparedDefinitionOrBodyUnwindsAndReturnsAllowance(string path)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(Source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = Seed(new DelayedLythonHost());
            var started = host.PauseReadUntilCancellation(path);
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { CancellationToken = cancellation.Token });
            try
            {
                await started.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
            }
            finally
            {
                await cancellation.CancelAsync();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(Seed(new DelayedLythonHost())))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(new List<object?> { new BigInteger(14), new List<object?> { new BigInteger(4) } }, result.ReturnValue);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static DelayedLythonHost Seed(DelayedLythonHost host)
    {
        host.SeedFile("/decorate", "ready");
        host.SeedFile("/body", "ready");
        return host;
    }

    private static MockLythonHost Seed(MockLythonHost host)
    {
        host.SeedFile("/decorate", "ready");
        host.SeedFile("/body", "ready");
        return host;
    }
}
