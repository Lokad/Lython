using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG11: explicit slice() calls own their shell; slice syntax never
/// materializes an object and stays free.
/// </summary>
public sealed class SliceObjectAccountingScenarioTests
{
    // 30k retained slices own 64B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix.
    private const long SliceBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedSlicesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            objs = []
            i = 0
            while i < 30000:
                objs.append(slice(1, 50))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SliceBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= SliceBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= SliceBudgetBytes);
    }

    [Fact]
    public async Task SliceObjectsBehave()
    {
        var script = new LythonEngine().Compile("""
            s = slice(1, 3)
            t = slice(None, None, 2)
            return ["abcdef"[s], "abcdef"[t], s.start, s.stop, t.step]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "bc", "ace", new BigInteger(1), new BigInteger(3), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}