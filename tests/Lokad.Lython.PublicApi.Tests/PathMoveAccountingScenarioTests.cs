using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: rename/replace own string-converted targets beside the move itself,
/// so retained results accumulate instead of riding the argument budget-free.
/// Path arguments keep aliasing the caller-owned value.
/// </summary>
public sealed class PathMoveAccountingScenarioTests
{
    // 20k retained move targets own ~130B plus a 64B shell and a 16B list
    // slot each, so they fit 1.5MB pre-fix and trip post-fix. Alternating two
    // hoisted paths keeps the file present while isolating target charges.
    private const long MoveBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedRenamesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            pa = Path("/a")
            pb = Path("/b")
            objs = []
            i = 0
            while i < 20000:
                if i % 2 == 0:
                    objs.append(pa.rename("/b"))
                else:
                    objs.append(pb.rename("/a"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MoveBudgetBytes };
        var host = new MockLythonHost();
        host.SeedFile("/a", "x");
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= MoveBudgetBytes);

        var rhost = new MockLythonHost();
        rhost.SeedFile("/a", "x");
        var asyncResult = await script.RunAsync(rhost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= MoveBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedReplacesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            pa = Path("/a")
            pb = Path("/b")
            objs = []
            i = 0
            while i < 20000:
                if i % 2 == 0:
                    objs.append(pa.replace("/b"))
                else:
                    objs.append(pb.replace("/a"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = MoveBudgetBytes };
        var host = new MockLythonHost();
        host.SeedFile("/a", "x");
        var sync = script.Run(host, options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= MoveBudgetBytes);

        var rhost = new MockLythonHost();
        rhost.SeedFile("/a", "x");
        var asyncResult = await script.RunAsync(rhost, options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= MoveBudgetBytes);
    }

    [Fact]
    public async Task PathMovesBehave()
    {
        var script = new LythonEngine().Compile("""
            from pathlib import Path
            r1 = Path("/a").rename("/b")
            r2 = Path("/b").replace(Path("/c"))
            return [r1.as_posix(), r2.as_posix(), Path("/c").read_text()]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "/b", "/c", "x" };
        var host = new MockLythonHost();
        host.SeedFile("/a", "x");
        var sync = script.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var rhost = new MockLythonHost();
        rhost.SeedFile("/a", "x");
        var asyncResult = await script.RunAsync(rhost);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
