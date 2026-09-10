using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG20/MG17: style copies own one 128B unit per allocated value like the
/// factories do, so retained copies accumulate instead of riding the source
/// budget-free. Nested members charge recursively through the same path.
/// </summary>
public sealed class StyleCopyAccountingScenarioTests
{
    // 20k retained font copies own 128B plus a 16B list slot each, so they
    // fit 1.5MB pre-fix and trip post-fix. A 5k border run owns five values
    // per copy and trips the same budget.
    private const long CopyBudgetBytes = 1572864;

    [Fact]
    public async Task ManyDeepCopiedFontsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import copy
            from openpyxl.styles import Font
            f = Font(bold=True)
            objs = []
            i = 0
            while i < 20000:
                objs.append(copy.deepcopy(f))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = CopyBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= CopyBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= CopyBudgetBytes);
    }

    [Fact]
    public async Task ManyShallowCopiedFontsStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import copy
            from openpyxl.styles import Font
            f = Font(bold=True)
            objs = []
            i = 0
            while i < 20000:
                objs.append(copy.copy(f))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = CopyBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= CopyBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= CopyBudgetBytes);
    }

    [Fact]
    public async Task ManyDeepCopiedBordersStayCharged()
    {
        var script = new LythonEngine().Compile("""
            import copy
            from openpyxl.styles import Border, Side
            b = Border(left=Side(style="thin"), right=Side(style="thin"), top=Side(style="thin"), bottom=Side(style="thin"))
            objs = []
            i = 0
            while i < 5000:
                objs.append(copy.deepcopy(b))
                i = i + 1
            return len(objs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = CopyBudgetBytes };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= CopyBudgetBytes);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= CopyBudgetBytes);
    }

    [Fact]
    public async Task StyleCopiesBehave()
    {
        var script = new LythonEngine().Compile("""
            import copy
            from openpyxl.styles import Border, Font, Side
            f = copy.deepcopy(Font(bold=True))
            g = copy.copy(Font(bold=True))
            b = copy.deepcopy(Border(left=Side(style="thin")))
            return [f.bold, g.bold, b.left.style]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, "thin" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
