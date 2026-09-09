using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG19: Differ fancy-tag scratch (per-line-pair tag builders plus the
/// whitespace rebuilds and QFormat intermediates) rides a transient
/// reservation beside the governed outputs. One long similar line pair keeps
/// committed outputs small while the tag scratch scales with line length.
/// </summary>
public sealed class FancyTagScratchAccountingScenarioTests
{
    private const string BuildFancyTags =
        """
        import difflib
        a = ["y" * 2000]
        b = ["y" * 1999 + "z"]
        d = list(difflib.Differ().compare(a, b))
        return [len(d), len(d[0])]
        """;

    [Fact]
    public async Task FancyTagScratchStaysCharged()
    {
        var script = new LythonEngine().Compile(BuildFancyTags);
        Assert.True(script.IsValid);
        // Calibration: pre-fix peak is 201591, so this fits; the per-pair
        // scratch backstop (24 x 4000 UTF-16 units) pushes the post-fix peak over.
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 229376 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task RoomyFancyTagsSucceed()
    {
        var script = new LythonEngine().Compile(BuildFancyTags);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 524288 };
        var expected = new List<object?> { new BigInteger(4), new BigInteger(2002) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FancyTagBehaviorStaysCompatible()
    {
        var script = new LythonEngine().Compile(
            """
            import difflib
            d = list(difflib.Differ().compare(["abcde"], ["abcXe"]))
            return d
            """);
        Assert.True(script.IsValid);
        var expected = new List<object?> { "- abcde", "?    ^\n", "+ abcXe", "?    ^\n" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
