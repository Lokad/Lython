using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: differ/html shells own lifetime like the other factory results do.
public class DifflibShellLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task DifferCtorDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nfor i in range(" + BatchIterations(50000) + "):\n    d = difflib.Differ()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task HtmlDiffCtorDiscardCompletes()
        => await AssertCompletes(
            "import difflib\nfor i in range(" + BatchIterations(50000) + "):\n    h = difflib.HtmlDiff()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DiffShellsBehave()
        => await AssertCompletes(
            "import difflib\nd = difflib.Differ()\nh = difflib.HtmlDiff(2)\nreturn str(list(d.compare(['a'], ['b']))) + str(h.make_table(['a'], ['b']).find('table') >= 0)\n", "['- a', '+ b']True");

    [Fact]
    public async Task RetainedDifferDenied()
    {
        var script = new LythonEngine().Compile(
            "import difflib\nobjs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(difflib.Differ())\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(RetentionBudget) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RetentionBudget);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RetentionBudget);
    }

    [Fact]
    public async Task RetainedHtmlDiffDenied()
    {
        var script = new LythonEngine().Compile(
            "import difflib\nobjs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(difflib.HtmlDiff())\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(RetentionBudget) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RetentionBudget);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RetentionBudget);
    }
}
