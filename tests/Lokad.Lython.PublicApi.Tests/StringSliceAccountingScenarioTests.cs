using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG03/MG06: subscript slices built from unowned receivers adopt ownership
/// like string method results; slices of owned receivers were already owned.
/// </summary>
public sealed class StringSliceAccountingScenarioTests
{
    // 20k retained slices own ~130B plus a 16B list slot each, so they fit
    // 1.5MB pre-fix and trip post-fix, on both the step-1 and stepped paths.
    private const long SliceBudgetBytes = 1572864;

    [Fact]
    public async Task ManyRetainedSlicesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            objs = []
            i = 0
            while i < 20000:
                objs.append("abcdef"[1:3])
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
    public async Task ManyRetainedSteppedSlicesStayCharged()
    {
        var script = new LythonEngine().Compile("""
            objs = []
            i = 0
            while i < 20000:
                objs.append("abcdefghij"[::2])
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = SliceBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task StringSlicesBehave()
    {
        var script = new LythonEngine().Compile("""
            s = "abcdefghij"
            return [s[1:3], s[::2], s[::-1], s[5:], s[:3], s[:], s[3:3]]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "bc", "acegi", "jihgfedcba", "fghij", "abc", "abcdefghij", "" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}