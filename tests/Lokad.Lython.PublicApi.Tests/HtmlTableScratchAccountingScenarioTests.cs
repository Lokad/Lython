using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// HTML rendering admits live UTF-8 capacity and replacement overlap, alongside
/// matcher state and final output. Aliased input lines keep payload input small.
/// </summary>
public sealed class HtmlTableScratchAccountingScenarioTests
{
    private const string BuildBigTable =
        """
        import difflib
        line = "x" * 2000
        lines = [line] * 600
        h = difflib.HtmlDiff()
        t = h.make_table(lines, lines)
        return len(t)
        """;

    [Fact]
    public async Task TableScratchStaysCharged()
    {
        using var script = new LythonEngine().Compile(BuildBigTable);
        Assert.True(script.IsValid);
        // The 2.5 MB final table fits on its own; live builder capacity and
        // replacement/copy overlap must also fit before allocation proceeds.
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, sync.FailureAttribution?.Reason);
        sync.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, asyncResult.FailureAttribution?.Reason);
        asyncResult.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RoomyTableSucceeds()
    {
        using var script = new LythonEngine().Compile(BuildBigTable);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(12 * 1024 * 1024);
        var options = new LythonRunOptions { MemoryBudget = budget };
        var expected = new BigInteger(2506652);
        using var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        sync.Dispose();
        Assert.Equal(0, budget.ReservedBytes);

        using var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        asyncResult.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
