using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: guest-mutated workbook cells own their table slots, so building many
/// cells in Python cannot bypass the execution memory budget. Loaded cells
/// stay under R02 package accounting.
/// </summary>
public sealed class WorkbookMutationAccountingScenarioTests
{
    [Fact]
    public async Task ManyMutatedCellsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            i = 1
            while i <= 10000:
                ws.cell(row=i, column=1, value=i)
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
    public async Task CellMutationBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            ws.cell(row=1, column=1, value=5)
            ws.cell(row=1, column=1, value=6)
            ws["B2"] = "hi"
            ws["C3"] = None
            ws.append([7, 8])
            return [ws.cell(row=1, column=1).value, ws["B2"].value, ws.max_row, ws.max_column]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(6), "hi", new BigInteger(3), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}