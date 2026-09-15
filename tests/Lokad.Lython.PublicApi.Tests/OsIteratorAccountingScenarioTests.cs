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

    // Discarded factory shells must reclaim through the pool instead of
    // stranding their charges: 100k abandoned creations complete at 3 MiB.
    [Fact]
    public async Task DiscardedWalkIteratorsComplete()
    {
        var script = new LythonEngine().Compile("""
            import os
            i = 0
            while i < 100000:
                x = os.walk("/d")
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3 * 1024 * 1024 };
        var sync = script.Run(SeededDirHost(3), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(SeededDirHost(3), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task DiscardedScandirIteratorsComplete()
    {
        var script = new LythonEngine().Compile("""
            import os
            i = 0
            while i < 100000:
                x = os.scandir("/d")
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3 * 1024 * 1024 };
        var sync = script.Run(SeededDirHost(3), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(SeededDirHost(3), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task DiscardedListdirResultsComplete()
    {
        var script = new LythonEngine().Compile("""
            import os
            i = 0
            while i < 100000:
                x = os.listdir("/d")
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3 * 1024 * 1024 };
        var sync = script.Run(SeededDirHost(3), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(SeededDirHost(3), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task ExhaustedScandirReclaims()
    {
        var script = new LythonEngine().Compile("""
            import os
            i = 0
            while i < 100000:
                for e in os.scandir("/d"):
                    pass
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3 * 1024 * 1024 };
        var sync = script.Run(SeededDirHost(3), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(SeededDirHost(3), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    // Entries escaping into guest code stay charged after their iterator
    // drops: keeping every entry of a 3000-file directory denies 100 KB.
    [Fact]
    public async Task RetainedScandirEntriesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            it = os.scandir("/d")
            kept = [e for e in it]
            it = None
            return len(kept)
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

    // Walk yields track individually at production (tuples, lists, and name
    // strings each carry a coupon), so fully consumed or abandoned walks
    // reclaim while unpacked outputs retained past their tuple stay charged.
    // Volume consumption needs a raised host-call budget: each walk makes
    // several host calls, which the default host-call limit counts separately
    // from memory.
    [Fact]
    public async Task ExhaustedWalkReclaims()
    {
        var script = new LythonEngine().Compile("""
            import os
            i = 0
            while i < 100000:
                for t in os.walk("/d"):
                    pass
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3 * 1024 * 1024, MaxHostCalls = 1000000 };
        var sync = script.Run(SeededDirHost(3), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(SeededDirHost(3), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    // Partial consumption without abrupt control: pull one tuple per walk
    // with next() and drop everything. (Abandonment via for/break at volume
    // is a separate pre-existing reclamation-cadence defect, os-independent:
    // even list displays strand that way. See the P01 residuals.)
    [Fact]
    public async Task AbandonedWalkAfterFirstPullReclaims()
    {
        var script = new LythonEngine().Compile("""
            import os
            i = 0
            while i < 100000:
                it = os.walk("/d")
                t = next(it)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3 * 1024 * 1024, MaxHostCalls = 1000000 };
        var sync = script.Run(SeededDirHost(3), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(SeededDirHost(3), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    // Abrupt abandonment drops the loop iterator slot at the break edge
    // (executable for-loops used to root it forever); 100k iterations complete.
    [Fact]
    public async Task AbandonedWalkAfterBreakReclaims()
    {
        var script = new LythonEngine().Compile("""
            import os
            i = 0
            while i < 100000:
                for t in os.walk("/d"):
                    break
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 3 * 1024 * 1024, MaxHostCalls = 1000000 };
        var sync = script.Run(SeededDirHost(3), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(SeededDirHost(3), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task RetainedWalkUnpackingStaysCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            for d, dirs, files in os.walk("/d"):
                kept = files
            return len(kept)
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
    public async Task DeniedWalkYieldRecoversWhenFunded()
    {
        var script = new LythonEngine().Compile("""
            import os
            n = 0
            for t in os.walk("/d"):
                n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var denied = script.Run(SeededDirHost(3), new LythonRunOptions { MaxExecutionMemoryBytes = 100 });
        Assert.False(denied.Success);
        Assert.Equal("MemoryError", denied.Failure?.ExceptionType);

        var funded = script.Run(SeededDirHost(3));
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(1), funded.ReturnValue);

        var fundedAsync = await script.RunAsync(SeededDirHost(3));
        Assert.True(fundedAsync.Success, fundedAsync.Failure?.Message);
        Assert.Equal(new BigInteger(1), fundedAsync.ReturnValue);
    }

    [Fact]
    public async Task DeniedScandirRecoversWhenFunded()
    {
        var script = new LythonEngine().Compile("""
            import os
            n = 0
            for e in os.scandir("/d"):
                n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var denied = script.Run(SeededDirHost(3), new LythonRunOptions { MaxExecutionMemoryBytes = 100 });
        Assert.False(denied.Success);
        Assert.Equal("MemoryError", denied.Failure?.ExceptionType);

        var funded = script.Run(SeededDirHost(3));
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(3), funded.ReturnValue);

        var fundedAsync = await script.RunAsync(SeededDirHost(3));
        Assert.True(fundedAsync.Success, fundedAsync.Failure?.Message);
        Assert.Equal(new BigInteger(3), fundedAsync.ReturnValue);
    }
}