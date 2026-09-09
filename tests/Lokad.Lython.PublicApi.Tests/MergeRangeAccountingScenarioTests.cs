using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: merged-range registries own their entries, so many merges cannot
/// bypass the execution memory budget. Loaded ranges stay under R02 package
/// accounting.
/// </summary>
public sealed class MergeRangeAccountingScenarioTests
{
    [Fact]
    public async Task ManyMergesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            i = 1
            while i <= 2000:
                ws.merge_cells(start_row=i, start_column=1, end_row=i, end_column=2)
                i = i + 1
            return 0
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task MergeAndUnmergeBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            ws.merge_cells("A1:A2")
            ws.merge_cells("B1:B2")
            ws.unmerge_cells("A1:A2")
            return ws.merged_cell_ranges
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "B1:B2" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}