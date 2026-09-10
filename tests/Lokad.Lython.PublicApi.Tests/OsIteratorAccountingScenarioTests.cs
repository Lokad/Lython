using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: directory iteration owns its iterator plus one entry object and
/// array slot per entry; walk iterators own their constructed-value unit.
/// </summary>
public sealed class OsIteratorAccountingScenarioTests
{
    // 3000 scandir entries own 160B plus 80B each, so they fit 100KB pre-fix
    // and trip post-fix. 20k walk iterators own 128B plus a 16B slot each.
    private const long ScandirBudgetBytes = 100000;
    private const long WalkBudgetBytes = 1572864;

    private static MockLythonHost SeededDirHost(int files)
    {
        var host = new MockLythonHost();
        for (var i = 0; i < files; i++)
        {
            host.SeedFile("/d/f" + i, "x");
        }

        return host;
    }

    [Fact]
    public async Task ManyScandirEntriesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            it = os.scandir("/d")
            n = 0
            for e in it:
                n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ScandirBudgetBytes };
        var sync = script.Run(SeededDirHost(3000), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ScandirBudgetBytes);

        var asyncResult = await script.RunAsync(SeededDirHost(3000), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ScandirBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedWalkIteratorsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.walk("/d"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = WalkBudgetBytes };
        var sync = script.Run(SeededDirHost(3), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(SeededDirHost(3), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DirectoryIterationBehaves()
    {
        var script = new LythonEngine().Compile("""
            import os
            names = sorted([e.name for e in os.scandir("/d")])
            w = [x for x in os.walk("/d")]
            return [names, len(w), w[0][0]]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new List<object?> { "f0", "f1", "f2" }, new BigInteger(1), "/d" };
        var sync = script.Run(SeededDirHost(3));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededDirHost(3));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}