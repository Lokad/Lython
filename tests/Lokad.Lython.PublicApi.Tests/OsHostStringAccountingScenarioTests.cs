using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG22/MG03: host-provided strings (working directory, environment values,
/// PATH entries) own their payload at conversion, so retained copies
/// accumulate instead of riding the host-owned source budget-free. The
/// environment table itself stays host-owned.
/// </summary>
public sealed class OsHostStringAccountingScenarioTests
{
    // 20k retained Cwd/value strings own ~135B plus a 16B list slot each, so
    // they fit 1.5MB pre-fix and trip post-fix. Split PATH entries trip 8MB.
    private const long StringBudgetBytes = 1572864;
    private const long ExecPathBudgetBytes = 8388608;

    private static LythonRunOptions OptionsWith(
        System.Collections.Generic.Dictionary<string, string> environment, long budget)
        => new() { Environment = environment, MaxExecutionMemoryBytes = budget };

    [Fact]
    public async Task ManyRetainedCwdsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.getcwd())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = OptionsWith(new(), StringBudgetBytes);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= StringBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= StringBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedGetenvsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.getenv("K"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var environment = new System.Collections.Generic.Dictionary<string, string> { ["K"] = "some-value" };
        var options = OptionsWith(environment, StringBudgetBytes);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= StringBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= StringBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedExecPathsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.get_exec_path())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var environment = new System.Collections.Generic.Dictionary<string, string> { ["PATH"] = "aaa:bbb:ccc" };
        var options = OptionsWith(environment, ExecPathBudgetBytes);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ExecPathBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ExecPathBudgetBytes);
    }

    [Fact]
    public async Task HostStringsBehave()
    {
        var script = new LythonEngine().Compile("""
            import os
            return [os.getcwd(), os.getenv("K"), os.get_exec_path(), os.getenv("MISSING", "dflt")]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "/", "V", new List<object?> { "aaa", "bbb" }, "dflt" };
        var environment = new System.Collections.Generic.Dictionary<string, string> { ["K"] = "V", ["PATH"] = "aaa:bbb" };
        var sync = script.Run(new MockLythonHost(), OptionsWith(environment, 268435456));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), OptionsWith(environment, 268435456));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
