using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: find_spec results own their shell, member slots and payloads, so
/// retained specs accumulate instead of riding the discovery budget-free.
/// </summary>
public sealed class ImportlibSpecAccountingScenarioTests
{
    // 20k retained specs own ~1.1KB each on top of list backing, so they fit
    // 4MB pre-fix and trip post-fix.
    private const long SpecBudgetBytes = 4194304;

    [Fact]
    public async Task ManyRetainedSpecsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import importlib.util
            objs = []
            i = 0
            while i < 20000:
                objs.append(importlib.util.find_spec("sys"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SpecBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= SpecBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= SpecBudgetBytes);
    }

    [Fact]
    public async Task SpecValuesBehave()
    {
        var script = new LythonEngine().Compile("""
            import importlib.util
            s = importlib.util.find_spec("sys")
            return [s.name, s.origin, s.has_location, s.parent, importlib.util.resolve_name(".n", "m")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "sys", "built-in", false, "", "m.n" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
