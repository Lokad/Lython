using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: worksheet row tuples and table/conditional-formatting collection
/// views own their backing and shells instead of escaping. Twenty thousand
/// retained rows must exceed a 4MB budget, wrappers a 1MB budget and iterated
/// keys a 6MB budget in both modes; pre-fix all three fit in ~0.6-4.5MB of
/// backing storage alone.
/// </summary>
public sealed class WorksheetViewAccountingScenarioTests
{
    private const long RowsBudgetBytes = 4194304;
    private const long WrapperBudgetBytes = 1048576;
    private const long IterateBudgetBytes = 6291456;


    [Fact]
    public async Task ManyRetainedRowsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import openpyxl
            wb = openpyxl.Workbook()
            data = wb.active
            data["A1"] = 1
            data["B2"] = 2
            from openpyxl.worksheet.table import Table
            data.add_table(Table(displayName="Inventory", ref="A1:B3"))
            objs = []
            i = 0
            while i < 20000:
                objs.append(data.rows)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RowsBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RowsBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RowsBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedWrappersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import openpyxl
            wb = openpyxl.Workbook()
            data = wb.active
            data["A1"] = 1
            data["B2"] = 2
            from openpyxl.worksheet.table import Table
            data.add_table(Table(displayName="Inventory", ref="A1:B3"))
            objs = []
            i = 0
            while i < 20000:
                objs.append(data.tables)
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = WrapperBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= WrapperBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= WrapperBudgetBytes);
    }

    [Fact]
    public async Task ManyRetainedIteratedKeysStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import openpyxl
            wb = openpyxl.Workbook()
            data = wb.active
            data["A1"] = 1
            data["B2"] = 2
            from openpyxl.worksheet.table import Table
            data.add_table(Table(displayName="Inventory", ref="A1:B3"))
            objs = []
            i = 0
            while i < 20000:
                objs.append(list(data.tables))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = IterateBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= IterateBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= IterateBudgetBytes);
    }

    [Fact]
    public async Task WorksheetViewsBehave()
    {
        var script = new LythonEngine().Compile("""
            import openpyxl
            wb = openpyxl.Workbook()
            data = wb.active
            data["A1"] = 1
            data["B2"] = 2
            from openpyxl.worksheet.table import Table
            data.add_table(Table(displayName="Inventory", ref="A1:B3"))
            rows = list(data.rows)
            return [
                rows[0][0].value, rows[1][1].value,
                list(data.tables.keys()), list(data.tables)[0],
                list(data.conditional_formatting.ranges),
                list(data.conditional_formatting.items()),
            ]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?>
        {
            new BigInteger(1), new BigInteger(2),
            new List<object?> { "Inventory" }, "Inventory",
            new List<object?>(), new List<object?>(),
        };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}