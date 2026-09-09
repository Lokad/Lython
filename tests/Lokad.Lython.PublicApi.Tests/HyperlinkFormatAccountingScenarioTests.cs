using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: hyperlink and number-format tables own their entries, so annotating
/// many cells cannot bypass the execution memory budget. A shared constant
/// isolates table growth: inline literals would allocate per iteration.
/// </summary>
public sealed class HyperlinkFormatAccountingScenarioTests
{
    [Fact]
    public async Task ManyHyperlinksStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            h = "x"
            i = 1
            while i <= 1500:
                ws.cell(row=i, column=1).hyperlink = h
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
    public async Task ManyNumberFormatsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            f = "0.00"
            i = 1
            while i <= 1500:
                ws.cell(row=i, column=1).number_format = f
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
    public async Task HyperlinkAndFormatBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            wb = openpyxl.Workbook()
            ws = wb.active
            ws["A1"].hyperlink = "https://www.lokad.com/"
            ws["A1"].number_format = "0.00"
            ws["A1"].hyperlink = "https://example.test/"
            return [ws["A1"].hyperlink.target, ws["A1"].number_format]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "https://example.test/", "0.00" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
