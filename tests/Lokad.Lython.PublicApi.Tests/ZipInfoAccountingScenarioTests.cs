using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11/MG03: the ZipInfo factory owns its shell plus the filename and
/// date_time payloads, so retained infos accumulate instead of riding the
/// argument budget-free. Directory-backed infos ride the entry base charge.
/// </summary>
public sealed class ZipInfoAccountingScenarioTests
{
    // 20k retained infos own a shell plus payloads on top of list backing, so
    // they fit 5MB pre-fix and trip post-fix. The explicit date_time literal
    // contributes a governed transient in both modes.
    private const long InfoBudgetBytes = 5242880;

    [Fact]
    public async Task ManyExplicitInfosStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import zipfile
            objs = []
            i = 0
            while i < 20000:
                objs.append(zipfile.ZipInfo("a.txt", (2020, 1, 2, 3, 4, 5)))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = InfoBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= InfoBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= InfoBudgetBytes);
    }

    [Fact]
    public async Task ManyDefaultInfosStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import zipfile
            objs = []
            i = 0
            while i < 20000:
                objs.append(zipfile.ZipInfo())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = InfoBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= InfoBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= InfoBudgetBytes);
    }

    [Fact]
    public async Task ZipInfoFactoryBehaves()
    {
        var script = new LythonEngine().Compile("""
            import zipfile
            info = zipfile.ZipInfo("a.txt", (2020, 1, 2, 3, 4, 5))
            dflt = zipfile.ZipInfo()
            return [info.filename, info.date_time, dflt.filename, dflt.date_time]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            "a.txt",
            new List<object?> { new BigInteger(2020), new BigInteger(1), new BigInteger(2), new BigInteger(3), new BigInteger(4), new BigInteger(5) },
            "NoName",
            new List<object?> { new BigInteger(1980), new BigInteger(1), new BigInteger(1), new BigInteger(0), new BigInteger(0), new BigInteger(0) },
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
