using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: per-cell style assignments own their table slots, so styling many
/// cells cannot bypass the execution memory budget. A shared style object
/// isolates table growth.
/// </summary>
public sealed class CellStyleAccountingScenarioTests
{
    [Fact]
    public async Task ManyStyledCellsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.styles import Font
            wb = openpyxl.Workbook()
            ws = wb.active
            f = Font(bold=True)
            i = 1
            while i <= 1500:
                ws.cell(row=i, column=1).font = f
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
    public async Task StyleBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.styles import Font
            wb = openpyxl.Workbook()
            ws = wb.active
            ws["A1"].font = Font(bold=True)
            ws["A1"].font = Font(bold=False)
            return [ws["A1"].font.bold, ws["B2"].font.bold]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { false, false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}