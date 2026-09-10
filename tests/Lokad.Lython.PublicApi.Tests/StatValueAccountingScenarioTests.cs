using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: os.stat results own one box unit per call, so retained stats
/// accumulate instead of riding the call budget-free.
/// </summary>
public sealed class StatValueAccountingScenarioTests
{
    // 20k retained stats own 64B plus a 16B list slot each, so they fit 1MB
    // pre-fix and trip post-fix.
    private const long StatBudgetBytes = 1048576;

    private static MockLythonHost SeededHost()
    {
        var host = new MockLythonHost();
        host.SeedFile("/d/f0", "x");
        return host;
    }

    [Fact]
    public async Task ManyRetainedStatsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import os
            objs = []
            i = 0
            while i < 20000:
                objs.append(os.stat("/d/f0"))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = StatBudgetBytes };
        var sync = script.Run(SeededHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= StatBudgetBytes);

        var asyncResult = await script.RunAsync(SeededHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= StatBudgetBytes);
    }

    [Fact]
    public async Task StatValuesBehave()
    {
        var script = new LythonEngine().Compile("""
            import os
            st = os.stat("/d/f0")
            return [st.exists, st.is_file, st.is_dir, st.size]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, false, new BigInteger(1) };
        var sync = script.Run(SeededHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(SeededHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
