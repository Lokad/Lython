using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N07, part 2: random scratch (weight conversions, counted drains, Floyd
// position sets) lives in caller-scoped temporary reservations and releases
// on every exit path, including validation failure; sized counted
// populations serve picks by index with no drain. Before the fix, each
// discard loop below stranded scratch per call and denied its budget.
public sealed class RandomScratchLifetimeTests
{
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
        => await AssertCompletes(
            """
            import random
            for i in range(1000):
                random.sample(range(1000), 50, counts=[50]*1000)
            return "ok"
            """,
            "ok", 4194304);

    [Fact]
    public async Task FloydSampleDiscards_SucceedUnderBudget()
        => await AssertCompletes(
            """
            import random
            for i in range(1000):
                random.sample(range(1000), 3)
            return "ok"
            """,
            "ok", 1048576);

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
}