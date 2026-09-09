using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: worksheet copies duplicate every charged table, so copying an
/// annotated sheet cannot bypass the execution memory budget. Hoisted
/// constants isolate table growth from per-iteration literal costs; the
/// style and merge pool shares are pinned exactly by the white-box test.
/// </summary>
public sealed class WorksheetCopyAccountingScenarioTests
{
    [Fact]
    public async Task CopiedTablesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.styles import Font
            wb = openpyxl.Workbook()
            ws = wb.active
            f = Font(bold=True)
            fm = "0.00"
            h = "x"
            i = 1
            while i <= 200:
                c = ws.cell(row=i, column=1, value=i)
                c.font = f
                c.number_format = fm
                c.hyperlink = h
                i = i + 1
            cp = wb.copy_worksheet(ws)
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
    public async Task CopyBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            ws["A1"] = 5
            ws["A1"].number_format = "0.00"
            ws.merge_cells("A2:B2")
            cp = wb.copy_worksheet(ws)
            return [cp["A1"].value, cp["A1"].number_format, len(cp.merged_cell_ranges)]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(5), "0.00", new BigInteger(1) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}