using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: table-list views own their backing, tuples and key strings (values
/// alias registered tables) instead of escaping. Twenty thousand retained
/// items() results must exceed a 2MB budget in both modes; pre-fix they fit
/// in ~0.6MB of backing storage alone.
/// </summary>
public sealed class TableViewAccountingScenarioTests
{
    private const long ItemsBudgetBytes = 2097152;

    [Fact]
    public async Task ManyRetainedItemsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import openpyxl
            wb = openpyxl.Workbook()
            data = wb.active
            data["A1"] = 1
            from openpyxl.worksheet.table import Table
            data.add_table(Table(displayName="Inventory", ref="A1:B3"))
            objs = []
            i = 0
            while i < 20000:
                objs.append(data.tables.items())
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ItemsBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ItemsBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ItemsBudgetBytes);
    }

    [Fact]
    public async Task TableViewsBehave()
    {
        var script = new LythonEngine().Compile("""
            import openpyxl
            wb = openpyxl.Workbook()
            data = wb.active
            data["A1"] = 1
            from openpyxl.worksheet.table import Table
            data.add_table(Table(displayName="Inventory", ref="A1:B3"))
            vals = list(data.tables.values())
            items = list(data.tables.items())
            return [list(data.tables.keys()), vals[0].ref, items[0][0], items[0][1].ref, list(data.conditional_formatting.items())]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new List<object?> { "Inventory" }, "A1:B3", "Inventory", "A1:B3",
            new List<object?>(),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}