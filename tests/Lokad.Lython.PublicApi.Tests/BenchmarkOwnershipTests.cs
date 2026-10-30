using Lokad.Lython.Benchmarks;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BenchmarkOwnershipTests
{
    [Fact]
    public void TheActualBenchmarkHostSupportsSynchronousFileScripts()
    {
        var host = new BenchmarkHost();
        host.SeedUtf8("/input.txt", "line\n");
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("with open('/input.txt') as source:content=source.read()\nwith open('/output.txt','w') as target:target.write(content)\nreturn content");
        BenchmarkScripts.Run(script, host, new LythonRunOptions { MemoryBudget = budget }, value => Assert.Equal("line\n", value));
        Assert.Equal("line\n", System.Text.Encoding.UTF8.GetString(host.ReadTextUtf8("/output.txt").Span));
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BenchmarkResultsRemainFundedThroughConsumptionAndAlwaysReturnAllowance(bool consumerFails)
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("return 'value'", budget);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = budget };
        void Consume(object? value)
        {
            Assert.Equal("value", value);
            Assert.True(budget.ReservedBytes > baseline);
            if (consumerFails) throw new ArgumentException("consumer");
        }
        foreach (var asynchronous in new[] { false, true })
        {
            for (var i = 0; i < 3; i++)
            {
                async Task Run()
                {
                    if (asynchronous) await BenchmarkScripts.RunAsync(script, new MockLythonHost(), options, Consume);
                    else BenchmarkScripts.Run(script, new MockLythonHost(), options, Consume);
                }
                if (consumerFails) await Assert.ThrowsAsync<ArgumentException>(Run);
                else await Run();
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
    }

    [Fact]
    public async Task FailedBenchmarkRunsReleaseTheirFailureOwners()
    {
        var budget = new LythonMemoryBudget(262144);
        using var script = new LythonEngine().Compile("raise ValueError('benchmark')", budget);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = budget };
        Assert.Throws<InvalidOperationException>(() => BenchmarkScripts.Run(script, new MockLythonHost(), options));
        Assert.Equal(baseline, budget.ReservedBytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => BenchmarkScripts.RunAsync(script, new MockLythonHost(), options));
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
