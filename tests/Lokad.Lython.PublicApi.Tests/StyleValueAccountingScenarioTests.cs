using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: constructed style values own their object storage, so retaining many
/// styles cannot bypass the execution memory budget.
/// </summary>
public sealed class StyleValueAccountingScenarioTests
{
    [Fact]
    public async Task ManyStyleValuesStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            from openpyxl.styles import Font
            fs = []
            i = 0
            while i < 2000:
                fs.append(Font(bold=True))
                i = i + 1
            return len(fs)
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
    public async Task StyleValuesStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            from openpyxl.styles import Font
            f = Font(bold=True, size=12)
            return [f.bold, f.size, f.italic]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, 12.0, false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}