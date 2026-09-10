using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: the sys.modules snapshot owns its key strings, so repeated refreshes
/// accumulate instead of riding invisible. Twenty thousand refreshes with two
/// imports must exceed a 2MB budget in both modes; pre-fix they fit in ~0.6MB
/// of backing storage alone.
/// </summary>
public sealed class SysModulesAccountingScenarioTests
{
    private const long ModulesBudgetBytes = 2097152;

    [Fact]
    public async Task ManyRetainedSnapshotsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import sys
            import math
            import json
            objs = []
            i = 0
            while i < 20000:
                objs.append(sys.modules)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ModulesBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ModulesBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ModulesBudgetBytes);
    }

    [Fact]
    public async Task SysModulesBehave()
    {
        var script = new LythonEngine().Compile("""
            import sys
            import math
            first = sys.modules
            second = sys.modules
            return [first is second, "math" in second, "nosuchmod_xyz" in second]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}