using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: directory listing names own their string payloads (listdir elements
/// directly, entry names through the entry-owned governor), so retained names
/// accumulate instead of riding the backing budget-free.
/// </summary>
public sealed class OsListingNameAccountingScenarioTests
{
    // 200 retained 100-name listings own ~135B per name on top of backing, so
    // they fit 1.5MB pre-fix and trip post-fix.
    private const long ListDirBudgetBytes = 1572864;
    // 3000 retained entry names own ~135B each on top of iterator backing, so
    // they fit 500KB pre-fix and trip post-fix.
    private const long EntryBudgetBytes = 500000;

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
    public async Task ManyRetainedListDirNamesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 200:
                objs.append(os.listdir("/d"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ListDirBudgetBytes };
        var sync = script.Run(SeededDirHost(100), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ListDirBudgetBytes);

        var asyncResult = await script.RunAsync(SeededDirHost(100), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ListDirBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedEntryNamesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            for e in os.scandir("/d"):
                objs.append(e.name)
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = EntryBudgetBytes };
        var sync = script.Run(SeededDirHost(3000), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= EntryBudgetBytes);

        var asyncResult = await script.RunAsync(SeededDirHost(3000), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= EntryBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedEntryPathsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            for e in os.scandir("/d"):
                objs.append(e.path)
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = EntryBudgetBytes };
        var sync = script.Run(SeededDirHost(3000), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= EntryBudgetBytes);

        var asyncResult = await script.RunAsync(SeededDirHost(3000), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= EntryBudgetBytes);
    }

    [Fact]
    public async Task ListingNamesBehave()
    {
        var script = new LythonEngine().Compile("""
            import os
            names = sorted(os.listdir("/d"))
            entries = sorted([[e.name, e.path] for e in os.scandir("/d")])
            return [names, entries]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { "f0.txt", "f1.txt" },
            new List<object?> { new List<object?> { "f0.txt", "/d/f0.txt" }, new List<object?> { "f1.txt", "/d/f1.txt" } },
        };
        var sync = script.Run(SeededDirHost(2));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededDirHost(2));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
