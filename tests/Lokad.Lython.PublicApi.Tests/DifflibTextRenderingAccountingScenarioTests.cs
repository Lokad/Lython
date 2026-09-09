using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG19: difflib text rendering owns its line payloads instead of retaining
/// uncharged copies.
/// </summary>
public sealed class DifflibTextRenderingAccountingScenarioTests
{
    [Fact]
    public async Task DifferOutputStaysCharged()
    {
        // Aliased long lines isolate rendering growth from matcher charges.
        var script = new LythonEngine().Compile(
            """
            import difflib
            line = "y" * 2000
            a = [line] * 40
            d = list(difflib.Differ().compare(a, a))
            return len(d)
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
    public async Task UnifiedDiffOutputStaysCharged()
    {
        var script = new LythonEngine().Compile(
            """
            import difflib
            a = ["y" * 2000] * 40
            b = ["z" * 2000] * 40
            u = list(difflib.unified_diff(a, b))
            return len(u)
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
    public async Task TextRenderingBehaviorsStillProject()
    {
        var script = new LythonEngine().Compile(
            """
            import difflib
            d = list(difflib.Differ().compare(["a", "b"], ["a", "c"]))
            u = list(difflib.unified_diff(["a", "b"], ["a", "c"]))
            return [d[0], u[0].startswith("---"), len(u) > 1]
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "  a", true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}