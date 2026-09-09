using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG19: the HTML table assembly scratch is covered alongside the owned
/// output. Aliased input lines isolate rendering growth: one shared payload
/// with per-line prepared copies, matcher state, assembly scratch and output.
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
        var script = new LythonEngine().Compile(BuildBigTable);
        Assert.True(script.IsValid);
        // Calibration: pre-fix peak fits (succeeds); the scratch backstop
        // pushes the post-fix peak over.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 6291456 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyTableSucceeds()
    {
        var script = new LythonEngine().Compile(BuildBigTable);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 12582912 };
        var expected = new BigInteger(2506652);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}