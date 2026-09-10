using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: pkgutil infos and loaders own a 64B shell beside governed names at
/// every construction site, so retained discoveries accumulate instead of
/// riding the list backing budget-free. Arguments keep aliasing.
/// </summary>
public sealed class PkgutilAccountingScenarioTests
{
    // 20k retained infos own 64B plus a 16B list slot each, so they fit 1MB
    // pre-fix and trip post-fix. Discovery lists add ~85 owned names each.
    private const long ValueBudgetBytes = 1048576;
    private const long IterBudgetBytes = 4194304;

    [Fact]
    public async Task ManyRetainedModuleInfosStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            objs = []
            i = 0
            while i < 20000:
                objs.append(pkgutil.ModuleInfo(None, "mod", False))
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
    public async Task ManyRetainedReplacementsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            mi = pkgutil.ModuleInfo(None, "mod", False)
            objs = []
            i = 0
            while i < 20000:
                objs.append(mi._replace())
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
    public async Task ManyRetainedDiscoveriesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            objs = []
            i = 0
            while i < 2000:
                objs.append(list(pkgutil.iter_modules()))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = IterBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= IterBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= IterBudgetBytes);
    }

    [Fact]
    public async Task PkgutilValuesBehave()
    {
        var script = new LythonEngine().Compile("""
            import pkgutil
            mi = pkgutil.ModuleInfo(None, "mod", True)
            sub = mi._replace(name="sub")
            names = sorted([m.name for m in pkgutil.iter_modules()])
            return [mi.name, mi.ispkg, sub.name, len(names) > 0, all(names)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "mod", true, "sub", true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
