using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: random generator objects own their shell beside the tiny counter
/// state while seeds stay inline.
/// </summary>
public sealed class RandomStateAccountingScenarioTests
{
    // 20k retained generators own 64B plus a 16B list slot each, so they fit
    // 1MB pre-fix and trip post-fix.
    private const long RandomBudgetBytes = 1048576;

    [Fact]
    public async Task ManyRetainedGeneratorsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import random
            objs = []
            i = 0
            while i < 20000:
                objs.append(random.Random())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RandomBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RandomBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RandomBudgetBytes);
    }

    [Fact]
    public async Task GeneratorSeedsBehave()
    {
        var script = new LythonEngine().Compile("""
            import random
            r = random.Random(42)
            a = r.random()
            r2 = random.Random(42)
            b = r2.random()
            return [a == b, r.random() == r.random()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}