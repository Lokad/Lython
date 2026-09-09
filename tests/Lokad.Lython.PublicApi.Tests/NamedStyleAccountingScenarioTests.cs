using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: named-style application owns its entries, so styling many cells via
/// a shared NamedStyle cannot bypass the execution memory budget. Hoisted
/// constants isolate table growth from per-iteration literal costs.
/// </summary>
public sealed class NamedStyleAccountingScenarioTests
{
    [Fact]
    public async Task ManyNamedStylesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.styles import Font, NamedStyle
            wb = openpyxl.Workbook()
            ws = wb.active
            ns = NamedStyle(name="M", number_format="0.00", font=Font(bold=True))
            i = 1
            while i <= 400:
                ws.cell(row=i, column=1).style = ns
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
    public async Task NamedStyleBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.styles import NamedStyle
            wb = openpyxl.Workbook()
            ws = wb.active
            ws["A1"].style = NamedStyle(name="M", number_format="0.00")
            return [ws["A1"].style, ws["A1"].number_format]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "M", "0.00" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}