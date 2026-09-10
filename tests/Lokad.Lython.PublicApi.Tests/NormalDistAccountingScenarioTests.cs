using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG15/MG11: NormalDist values own a 64B shell beside their free 64-bit
/// payloads at construction and at every arithmetic site, so retained
/// distributions accumulate instead of riding the source budget-free.
/// </summary>
public sealed class NormalDistAccountingScenarioTests
{
    // 20k retained distributions own 64B plus a 16B list slot each, so they
    // fit 1MB pre-fix and trip post-fix. The from_samples drain holds a small
    // governed transient, so that flip uses a 1.5MB budget instead.
    private const long ValueBudgetBytes = 1048576;
    private const long SamplesBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedDistributionsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import statistics
            objs = []
            i = 0
            while i < 20000:
                objs.append(statistics.NormalDist(0, 1))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ValueBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ValueBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ValueBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedSumsStayCharged()
    {
        var script = new LythonEngine().Compile("""
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
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ValueBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ValueBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ValueBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedNegationsStayCharged()
    {
        var script = new LythonEngine().Compile("""
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
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ValueBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ValueBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ValueBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedSamplesStayCharged()
    {
        var script = new LythonEngine().Compile("""
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
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SamplesBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= SamplesBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= SamplesBudgetBytes);
    }

    [Fact]
    public async Task NormalDistValuesBehave()
    {
        var script = new LythonEngine().Compile("""
            import statistics
            d = statistics.NormalDist(2, 4)
            e = statistics.NormalDist.from_samples([1, 2, 3, 4])
            return [d.mean, d.stdev, e.mean, (+d).mean, (d + 1).mean, (-d).mean]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { 2.0, 4.0, 2.5, 2.0, 3.0, -2.0 };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
