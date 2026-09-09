using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG19: difflib HTML rendering owns its prepared lines and output instead
/// of retaining uncharged copies.
/// </summary>
public sealed class DifflibRenderingAccountingScenarioTests
{
    [Fact]
    public async Task HtmlTableOutputStaysCharged()
    {
        // Aliased input lines isolate rendering growth: one shared payload
        // with per-line prepared copies and output.
        var script = new LythonEngine().Compile(
            """
            import difflib
            line = "x" * 2000
            lines = [line] * 20
            h = difflib.HtmlDiff()
            t = h.make_table(lines, lines)
            return len(t)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RenderingBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import difflib
            h = difflib.HtmlDiff()
            t = h.make_table(["a", "b"], ["a", "c"])
            f = h.make_file(["a"], ["a"], "from", "to")
            d = list(difflib.Differ().compare(["a", "b"], ["a", "c"]))
            return [t.startswith("<table"), f.startswith("<!DOCTYPE"), d[0]]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, "  a" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}