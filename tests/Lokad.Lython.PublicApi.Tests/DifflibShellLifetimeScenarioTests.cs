using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: differ/html shells own lifetime like the other factory results do.
public sealed class DifflibShellLifetimeScenarioTests
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
    public async Task DifferCtorDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nfor i in range(50000):\n    d = difflib.Differ()\nreturn 0\n", "0");

    [Fact]
    public async Task HtmlDiffCtorDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nfor i in range(50000):\n    h = difflib.HtmlDiff()\nreturn 0\n", "0");

    [Fact]
    public async Task DiffShellsBehave()
        => await AssertCompletes(
            "import difflib\nd = difflib.Differ()\nh = difflib.HtmlDiff(2)\nreturn str(list(d.compare(['a'], ['b']))) + str(h.make_table(['a'], ['b']).find('table') >= 0)\n", "['- a', '+ b']True");

    [Fact]
    public async Task RetainedDifferDenied()
    {
        var script = new LythonEngine().Compile(
            "import difflib\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(difflib.Differ())\n    i = i + 1\nreturn len(objs)\n");
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

    [Fact]
    public async Task RetainedHtmlDiffDenied()
    {
        var script = new LythonEngine().Compile(
            "import difflib\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(difflib.HtmlDiff())\n    i = i + 1\nreturn len(objs)\n");
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
