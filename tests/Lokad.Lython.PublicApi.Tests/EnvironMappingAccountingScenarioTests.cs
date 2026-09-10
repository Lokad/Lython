using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22: environment mapping reads convert through the stored governor, so
/// retained keys and values accumulate while the table itself stays
/// host-owned. Reads never alias across mutations (no cache by design).
/// </summary>
public sealed class EnvironMappingAccountingScenarioTests
{
    // 20k retained single values own ~130B plus a 16B list slot each, so they
    // fit 1.5MB pre-fix and trip post-fix. Literal subscript keys trip static
    // LA3157, so shapes read through loop variables and .get instead.
    private const long ValueBudgetBytes = 1572864;
    // 10k retained key loops add iteration scratch beside retained values.
    private const long GetItemBudgetBytes = 2097152;
    // 20k retained key lists own two strings each on top of backing, so they
    // fit 8MB pre-fix and trip post-fix.
    private const long KeysBudgetBytes = 8388608;
    // 20k retained item lists own four strings each on top of backing, so
    // they fit 12MB pre-fix and trip post-fix.
    private const long ItemsBudgetBytes = 12582912;

    private static LythonRunOptions OptionsWith(
        System.Collections.Generic.Dictionary<string, string> environment, long budget)
        => new() { Environment = environment, MaxExecutionMemoryBytes = budget };

    private static System.Collections.Generic.Dictionary<string, string> TestEnvironment()
        => new() { ["K1"] = "v1", ["K2"] = "v2" };

    [Fact]
    public async Task ManyRetainedGetsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            e = os.environ
            objs = []
            i = 0
            while i < 20000:
                objs.append(e.get("K1"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = OptionsWith(TestEnvironment(), ValueBudgetBytes);
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
    public async Task ManyRetainedSubscriptsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            e = os.environ
            objs = []
            i = 0
            while i < 10000:
                for k in e:
                    objs.append(e[k])
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = OptionsWith(TestEnvironment(), GetItemBudgetBytes);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= GetItemBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= GetItemBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedKeysStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            e = os.environ
            objs = []
            i = 0
            while i < 20000:
                objs.append(e.keys())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = OptionsWith(TestEnvironment(), KeysBudgetBytes);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= KeysBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= KeysBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedItemsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            e = os.environ
            objs = []
            i = 0
            while i < 20000:
                objs.append(e.items())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = OptionsWith(TestEnvironment(), ItemsBudgetBytes);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ItemsBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ItemsBudgetBytes);
    }

    [Fact]
    public async Task MappingReadsBehave()
    {
        var script = new LythonEngine().Compile("""
            import os
            e = os.environ
            return [e.get("K1"), sorted(e.keys()), sorted(e.values()), e.copy().get("K2"), sorted([k for k in e])]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "v1",
            new List<object?> { "K1", "K2" },
            new List<object?> { "v1", "v2" },
            "v2",
            new List<object?> { "K1", "K2" },
        };
        var sync = script.Run(new MockLythonHost(), OptionsWith(TestEnvironment(), 268435456));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), OptionsWith(TestEnvironment(), 268435456));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
