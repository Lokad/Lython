using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: ModuleInfo slices commit their backing through the info-owned
/// governor like tuple slices do, so retained slices accumulate instead of
/// riding the source budget-free. Elements keep aliasing.
/// </summary>
public sealed class PkgutilSliceAccountingScenarioTests
{
    // 20k retained slices own a 64B backing plus a 16B list slot each, so
    // they fit 1MB pre-fix and trip post-fix, matching tuple slices.
    private const long SliceBudgetBytes = 1048576;

    [Fact]
    public async Task ManyRetainedSlicesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            mi = pkgutil.ModuleInfo(None, "m", False)
            objs = []
            i = 0
            while i < 20000:
                objs.append(mi[0:2])
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SliceBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= SliceBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= SliceBudgetBytes);
    }

    [Fact]
    public async Task ModuleInfoSlicesBehave()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            mi = pkgutil.ModuleInfo(None, "m", True)
            return [mi[0:2], mi[1:], mi[2]]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new List<object?> { null, "m" }, new List<object?> { "m", true }, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
