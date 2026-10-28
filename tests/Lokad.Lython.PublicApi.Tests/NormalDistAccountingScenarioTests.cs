using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>Original-scale containment stress; focused model funding is checked separately.</summary>
public sealed class NormalDistAccountingScenarioTests
{
    private const long ValueBudgetBytes = 1048576;
    private const long SamplesBudgetBytes = 1572864;

    [Fact, Trait("Category", "Stress")]
    public async Task ManyRetainedDistributionsStayCharged()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import statistics
            objs = []
            i = 0
            while i < 20000:
                objs.append(statistics.NormalDist(0, 1))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(ValueBudgetBytes);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ValueBudgetBytes);
        sync.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ValueBudgetBytes);
        asyncResult.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact, Trait("Category", "Stress")]
    public async Task ManyRetainedSumsStayCharged()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import statistics
            d = statistics.NormalDist(0, 1)
            objs = []
            i = 0
            while i < 20000:
                objs.append(d + 1)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(ValueBudgetBytes);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ValueBudgetBytes);
        sync.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ValueBudgetBytes);
        asyncResult.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact, Trait("Category", "Stress")]
    public async Task ManyRetainedNegationsStayCharged()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import statistics
            d = statistics.NormalDist(0, 1)
            objs = []
            i = 0
            while i < 20000:
                objs.append(-d)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(ValueBudgetBytes);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ValueBudgetBytes);
        sync.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ValueBudgetBytes);
        asyncResult.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact, Trait("Category", "Stress")]
    public async Task ManyRetainedSamplesStayCharged()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import statistics
            data = [1, 2, 3]
            objs = []
            i = 0
            while i < 20000:
                objs.append(statistics.NormalDist.from_samples(data))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(SamplesBudgetBytes);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= SamplesBudgetBytes);
        sync.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= SamplesBudgetBytes);
        asyncResult.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task NormalDistValuesBehave()
    {
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("""
            import statistics
            d = statistics.NormalDist(2, 4)
            e = statistics.NormalDist.from_samples([1, 2, 3, 4])
            return [d.mean, d.stdev, e.mean, (+d).mean, (d + 1).mean, (-d).mean]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { 2.0, 4.0, 2.5, 2.0, 3.0, -2.0 };
        using var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        using var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
