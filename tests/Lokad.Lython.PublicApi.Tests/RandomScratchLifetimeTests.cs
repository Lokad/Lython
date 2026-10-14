using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N07, part 2: random scratch (weight conversions, counted drains, Floyd
// position sets) lives in caller-scoped temporary reservations and releases
// on every exit path, including validation failure; sized counted
// populations serve picks by index with no drain. Before the fix, each
// discard loop below stranded scratch per call and denied its budget.
public sealed class RandomScratchLifetimeTests : LifetimeScenario
{
    protected override int BatchRepetitions(int iterations) => Math.Max(8, iterations);

    private static LythonCompiledScript Compile(string source)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        return script;
    }

    private static async Task AssertCompletes(string source, string expected, long maxBytes)
    {
        var script = Compile(source);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = maxBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task CountedSampleDiscards_SucceedUnderBudget()
        => await AssertCountedSample(128, 16, 3, 16384);

    [Fact]
    [Trait("Category", "Stress")]
    public async Task CountedSampleStress()
        => await AssertCountedSample(1000, 1000, 50, 4194304);

    private static async Task AssertCountedSample(int iterations, int population, int picks, long budget)
        => await new RandomScratchLifetimeTests().AssertBatchCompletes(
            $"""
            import random
            for i in range(1):
                random.sample(range({population}), {picks}, counts=[50]*{population})
            return "ok"
            """,
            "ok", budget, iterations);

    [Fact]
    public async Task FloydSampleDiscards_SucceedUnderBudget()
        => await AssertFloydSample(64, 1000, 3, 65536);

    [Fact]
    [Trait("Category", "Stress")]
    public async Task FloydSampleStress()
        => await AssertFloydSample(1000, 1000, 3, 1048576);

    private static async Task AssertFloydSample(int iterations, int population, int picks, long budget)
        => await AssertCompletes(
            $"""
            import random
            for i in range({iterations}):
                random.sample(range({population}), {picks})
            return "ok"
            """,
            "ok", budget);

    [Fact]
    public async Task WeightedChoicesFailRecover_SucceedsUnderBudget()
        => await AssertCompletes(
            """
            import random
            pop = list(range(5000))
            w = [1]*4999+["x"]
            for i in range(20):
                try:
                    random.choices(pop,w,None,3)
                except (ValueError, TypeError):
                    pass
                random.choices([1,2],None,None,1)
            return "ok"
            """,
            "ok", 1572864);

    [Fact]
    public async Task CountedSampleReseed_ReproducesDraw()
    {
        // Index-based counted picks reuse the exact Floyd draws, so a
        // reseeded draw reproduces itself in both execution modes.
        var script = Compile("""
            import random
            random.seed(7)
            first = random.sample(range(1000), 50, counts=[50]*1000)
            random.seed(7)
            second = random.sample(range(1000), 50, counts=[50]*1000)
            return first == second
            """);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.True(sync.ReturnValue is true);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(sync.ReturnValue, asyncResult.ReturnValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedInvalidWeightDrainsReleaseTheirBacking(bool asyncMode)
    {
        var script = Compile("""
            import random
            pop = list(range(1000))
            weights = [1] * 999 + ["x"]
            for i in range(1000):
                try:
                    random.choices(pop, weights=weights, k=3)
                except (ValueError, TypeError):
                    pass
                random.choices([1, 2], k=1)
            return "ok"
            """);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var result = asyncMode
            ? await script.RunAsync(new MockLythonHost(), options)
            : script.Run(new MockLythonHost(), options);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ok", result.ReturnValue?.ToString());
    }
}
