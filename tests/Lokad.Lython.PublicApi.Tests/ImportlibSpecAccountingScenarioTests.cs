using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: find_spec results own their shell, member slots and payloads, so
/// retained specs accumulate instead of riding the discovery budget-free.
/// </summary>
public sealed class ImportlibSpecAccountingScenarioTests
{
    [Fact]
    public Task RetainedSpecsStayCharged() => AssertRetainedSpecsCharged(600, 262144);

    [Fact, Trait("Category", "Stress")]
    public Task ManyRetainedSpecsStayCharged() => AssertRetainedSpecsCharged(20000, 4194304);

    private static async Task AssertRetainedSpecsCharged(int count, long allowance)
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import importlib.util
            objs = []
            i = 0
            while i < count:
                objs.append(importlib.util.find_spec("sys"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(allowance);
        var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["count"] = count } };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= allowance);

        sync.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= allowance);
        asyncResult.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task SpecValuesBehave()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import importlib.util
            s = importlib.util.find_spec("sys")
            return [s.name, s.origin, s.has_location, s.parent, importlib.util.resolve_name(".n", "m")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "sys", "built-in", false, "", "m.n" };
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        using var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
