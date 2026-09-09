using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG17: the comment table owns its entries, so annotating many cells cannot
/// bypass the execution memory budget. A shared Comment isolates table growth:
/// inline construction would allocate per iteration. Distinct retained
/// Comments own their 128B object storage via the value factory.
/// </summary>
public sealed class CommentAccountingScenarioTests
{
    [Fact]
    public async Task ManyCommentsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.comments import Comment
            wb = openpyxl.Workbook()
            ws = wb.active
            c = Comment("note", "me")
            i = 1
            while i <= 1500:
                ws.cell(row=i, column=1).comment = c
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
    public async Task ManyConstructedCommentsStayCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.comments import Comment
            cs = []
            t = "t"
            a = "a"
            i = 0
            while i < 1500:
                cs.append(Comment(t, a))
                i = i + 1
            return len(cs)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult2 = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult2.Success);
        Assert.Equal("MemoryError", asyncResult2.Failure?.ExceptionType);
    }

    [Fact]
    public async Task CommentBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import openpyxl
            from openpyxl.comments import Comment
            wb = openpyxl.Workbook()
            ws = wb.active
            ws["A1"].comment = Comment("hello", "ada")
            ws["A1"].comment = Comment("bye", "grace")
            t = ws["A1"].comment.text
            a = ws["A1"].comment.author
            ws["A1"].comment = None
            cleared = ws["A1"].comment is None
            return [t, a, cleared]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "bye", "grace", true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
