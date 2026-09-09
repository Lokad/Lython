using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: row and column dimension entries own their storage, so touching many
/// dimensions cannot bypass the execution memory budget. Loaded dimensions
/// stay under R02 package accounting.
/// </summary>
public sealed class DimensionAccountingScenarioTests
{
    [Fact]
    public async Task ManyRowDimensionsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            i = 1
            while i <= 1500:
                d = ws.row_dimensions[i]
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
    public async Task ManyColumnDimensionsStayCharged()
    {
        // Distinct baked column names isolate registry growth; built names
        // would commit renderer charges of their own.
        static string ColumnName(int index)
        {
            var builder = new StringBuilder();
            var value = index;
            do
            {
                value--;
                builder.Insert(0, (char)('A' + (value % 26)));
                value /= 26;
            }
            while (value > 0);
            return builder.ToString();
        }

        var builder = new StringBuilder("import openpyxl\nwb = openpyxl.Workbook()\nws = wb.active\n");
        for (var i = 1; i <= 1500; i++)
        {
            builder.Append("d = ws.column_dimensions[\"").Append(ColumnName(i)).Append("\"]\n");
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
    public async Task DimensionBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            ws.row_dimensions[1].height = 20
            ws.column_dimensions["A"].width = 30
            return [ws.row_dimensions[1].height, ws.column_dimensions["A"].width]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { 20.0, 30.0 };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}