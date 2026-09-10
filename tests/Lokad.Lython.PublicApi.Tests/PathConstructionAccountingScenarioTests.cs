using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11/MG03: pathlib construction owns its string payload plus a 64B path
/// shell, and parents/parts collections own their element payloads beside
/// the governed backing, so retained paths and views accumulate instead of
/// riding the source budget-free.
/// </summary>
public sealed class PathConstructionAccountingScenarioTests
{
    // 20k retained paths own ~135B plus a 64B shell and a 16B list slot each,
    // so they fit 1.5MB pre-fix and trip post-fix.
    private const long FactoryBudgetBytes = 1572864;
    // 20k retained parents lists own two paths each on top of governed backing,
    // so they fit 8MB pre-fix and trip post-fix.
    private const long ParentsBudgetBytes = 8388608;
    // 20k retained parts tuples own three strings each on top of governed
    // backing, so they fit 5MB pre-fix and trip post-fix.
    private const long PartsBudgetBytes = 5242880;

    [Fact]
    public async Task ManyRetainedPathsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            objs = []
            i = 0
            while i < 20000:
                objs.append(Path("/a/b.txt"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = FactoryBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= FactoryBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= FactoryBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedParentsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            objs = []
            i = 0
            while i < 20000:
                objs.append(Path("/a/b/c").parents)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ParentsBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ParentsBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ParentsBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedPartsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            objs = []
            i = 0
            while i < 20000:
                objs.append(Path("/a/b/c").parts)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = PartsBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= PartsBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= PartsBudgetBytes);
    }

    [Fact]
    public async Task PathConstructionBehaves()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            p = Path("/a", "b/c")
            q = Path()
            return [p.as_posix(), q.as_posix(), str(Path("/a/b/c").parents), str(Path("/a/b/c").parts)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "/a/b/c", ".", "[/a/b, /a, /]", "('/', 'a', 'b', 'c')" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
