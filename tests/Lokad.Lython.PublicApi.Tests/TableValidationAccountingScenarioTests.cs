using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: constructed tables and validations own their object storage and
/// their worksheet registries own each entry, so many tables or validations
/// cannot bypass the execution memory budget. Baked display names isolate
/// registry growth: loop-built names would commit renderer charges of their
/// own, while a hoisted validation isolates list growth.
/// </summary>
public sealed class TableValidationAccountingScenarioTests
{
    [Fact]
    public async Task ManyTablesStayCharged()
    {
        // 500 tables at (128B object + 64B registry entry) exceed a 64KiB
        // budget; the baked names and refs commit nothing of their own.
        var builder = new StringBuilder("import openpyxl\nfrom openpyxl.worksheet.table import Table\nwb = openpyxl.Workbook()\nws = wb.active\n");
        for (var i = 0; i < 500; i++)
        {
            builder.Append("ws.add_table(Table(displayName=\"T").Append(i).Append("\", ref=\"A1:B2\"))\n");
        }

        builder.Append("return 0\n");
        var script = new LythonEngine().Compile(builder.ToString());
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
    public async Task ManyValidationsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.worksheet.datavalidation import DataValidation
            wb = openpyxl.Workbook()
            ws = wb.active
            dv = DataValidation(type="whole", formula1="1", formula2="10")
            i = 0
            while i < 1100:
                ws.add_data_validation(dv)
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
    public async Task TableAndValidationBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.worksheet.table import Table
            from openpyxl.worksheet.datavalidation import DataValidation
            wb = openpyxl.Workbook()
            ws = wb.active
            ws.add_table(Table(displayName="T1", ref="A1:B2"))
            ws.add_data_validation(DataValidation(type="whole", formula1="1", formula2="10"))
            return [ws.tables["T1"].ref, ws.data_validations.count]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "A1:B2", new BigInteger(1) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}