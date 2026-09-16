using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: rendered diff lines own lifetime like the other factory results do.
public sealed class DiffRenderLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task DifferCompareDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nd = difflib.Differ()\nfor i in range(50000):\n    x = list(d.compare(['a'], ['b']))\nreturn 0\n", "0");

    [Fact]
    public async Task NdiffDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nfor i in range(50000):\n    x = list(difflib.ndiff(['a'], ['b']))\nreturn 0\n", "0");

    [Fact]
    public async Task UnifiedDiffDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nfor i in range(20000):\n    x = list(difflib.unified_diff(['a'], ['b']))\nreturn 0\n", "0");

    [Fact]
    public async Task ContextDiffDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nfor i in range(20000):\n    x = list(difflib.context_diff(['a'], ['b']))\nreturn 0\n", "0");

    [Fact]
    public async Task DiffRenderBehaves()
        => await AssertCompletes(
            "import difflib\nreturn str(list(difflib.ndiff(['a'], ['b']))) + str(list(difflib.unified_diff(['a'], ['b']))[:2])\n", "['- a', '+ b']['--- \\n', '+++ \\n']");

    [Fact]
    public async Task RetainedDiffLinesDenied()
    {
        var script = new LythonEngine().Compile(
            "import difflib\nd = difflib.Differ()\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.extend(d.compare(['a'], ['b']))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = OneMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }
}
