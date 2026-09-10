using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11/MG03: pathlib derivations own their string payload plus a 64B path
/// shell beside the governed list backing, so retained names, parents and
/// joins accumulate instead of riding the source budget-free.
/// </summary>
public sealed class PathValueAccountingScenarioTests
{
    // 20k retained names own ~130B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix. Parents and joins add a 64B shell.
    private const long PathValueBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedNamesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            objs = []
            i = 0
            while i < 20000:
                objs.append(Path("/a/b.txt").name)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = PathValueBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= PathValueBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= PathValueBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedParentsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            objs = []
            i = 0
            while i < 20000:
                objs.append(Path("/a/b.txt").parent)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = PathValueBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= PathValueBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= PathValueBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedJoinsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            p = Path("/a")
            objs = []
            i = 0
            while i < 20000:
                objs.append(p / "b")
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = PathValueBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= PathValueBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= PathValueBudgetBytes);
    }

    [Fact]
    public async Task PathValuesBehave()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            base = Path("/repo/docs/page.md")
            return [base.name, base.suffix, base.stem, base.parent.as_posix(), base.joinpath("nested", "guide.txt").as_posix(), base.with_suffix(".txt").as_posix(), base.with_name("intro.md").as_posix(), base.relative_to("/repo").as_posix(), (Path("/a") / "b").as_posix()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "page.md", ".md", "page", "/repo/docs", "/repo/docs/page.md/nested/guide.txt", "/repo/docs/page.txt", "/repo/docs/intro.md", "docs/page.md", "/a/b" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
