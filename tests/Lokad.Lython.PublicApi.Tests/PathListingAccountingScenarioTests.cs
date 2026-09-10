using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: host directory listings own one path shell per entry beside the
/// governed payloads, so retained iterdir/glob/rglob results accumulate
/// instead of riding the directory backing budget-free.
/// </summary>
public sealed class PathListingAccountingScenarioTests
{
    // 3000 iterdir entries own ~200B each on top of list backing, so they fit
    // 300KB pre-fix and trip post-fix.
    private const long IterdirBudgetBytes = 300000;
    // 3000 glob entries add a 64B shell each on top of governed strings, so
    // they fit 640KB pre-fix and trip post-fix.
    private const long GlobBudgetBytes = 640000;
    // 101 rglob entries own ~200B each on top of list backing, so they fit
    // 12KB pre-fix and trip post-fix.
    private const long RglobBudgetBytes = 12000;

    private static MockLythonHost SeededDirHost(int files)
    {
        var host = new MockLythonHost();
        for (var i = 0; i < files; i++)
        {
            host.SeedFile("/d/f" + i + ".txt", "x");
        }

        return host;
    }

    [Fact]
    public async Task ManyIterdirEntriesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            it = list(Path("/d").iterdir())
            return len(it)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = IterdirBudgetBytes };
        var sync = script.Run(SeededDirHost(3000), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= IterdirBudgetBytes);

        var asyncResult = await script.RunAsync(SeededDirHost(3000), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= IterdirBudgetBytes);
    }

    [Fact]
    public async Task ManyGlobEntriesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            it = list(Path("/d").glob("*.txt"))
            return len(it)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = GlobBudgetBytes };
        var sync = script.Run(SeededDirHost(3000), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= GlobBudgetBytes);

        var asyncResult = await script.RunAsync(SeededDirHost(3000), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= GlobBudgetBytes);
    }

    [Fact]
    public async Task ManyRglobEntriesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            it = list(Path("/d").rglob("*.txt"))
            return len(it)
            """);
        Assert.True(script.IsValid);
        var host = SeededDirHost(100);
        host.SeedFile("/d/sub/g0.txt", "x");
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RglobBudgetBytes };
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RglobBudgetBytes);

        var rhost = SeededDirHost(100);
        rhost.SeedFile("/d/sub/g0.txt", "x");
        var asyncResult = await script.RunAsync(rhost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RglobBudgetBytes);
    }

    [Fact]
    public async Task PathListingsBehave()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            names = sorted([e.name for e in Path("/d").iterdir()])
            matches = sorted([p.as_posix() for p in Path("/d").glob("*.txt")])
            tree = sorted([p.as_posix() for p in Path("/d").rglob("*.txt")])
            return [names, matches, tree]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new List<object?> { "f0.txt", "f1.txt", "sub" }, new List<object?> { "/d/f0.txt", "/d/f1.txt" }, new List<object?> { "/d/f0.txt", "/d/f1.txt", "/d/sub/g0.txt" } };
        var host = SeededDirHost(2);
        host.SeedFile("/d/sub/g0.txt", "x");
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var rhost = SeededDirHost(2);
        rhost.SeedFile("/d/sub/g0.txt", "x");
        var asyncResult = await script.RunAsync(rhost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
