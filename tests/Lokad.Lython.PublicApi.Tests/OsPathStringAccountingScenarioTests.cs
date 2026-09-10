using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11/MG03: os.path string helpers own their fresh results, and split
/// tuples own their element strings beside the governed backing, so retained
/// paths accumulate instead of riding the source budget-free.
/// </summary>
public sealed class OsPathStringAccountingScenarioTests
{
    // 20k retained basenames own ~130B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix. The normpath shape is identical.
    private const long NameBudgetBytes = 1572864;
    // 20k retained split tuples own two strings each on top of governed
    // backing, so they fit 2.5MB pre-fix and trip post-fix.
    private const long SplitBudgetBytes = 2621440;

    [Fact]
    public async Task ManyRetainedBaseNamesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.path.basename("/a/b.txt"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = NameBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= NameBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= NameBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedNormPathsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.path.normpath("/a//b/./c"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = NameBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= NameBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= NameBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedSplitsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.path.split("/a/b.txt"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SplitBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= SplitBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= SplitBudgetBytes);
    }

    [Fact]
    public async Task OsPathStringsBehave()
    {
        var script = new LythonEngine().Compile("""
            import os
            return [os.path.basename("/a/b.txt"), os.path.dirname("/a/b.txt"), os.path.normpath("/a//b/./c"), os.path.splitext("/a/b.txt"), os.path.relpath("/a/b", "/a"), os.path.commonpath(["/a/b", "/a/c"])]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "b.txt", "/a", "/a/b/c", new List<object?> { "/a/b", ".txt" }, "b", "/a" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
