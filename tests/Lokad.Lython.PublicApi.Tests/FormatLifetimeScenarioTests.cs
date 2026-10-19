using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class FormatLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted(long budget) => new() { MemoryBudget = new LythonMemoryBudget(budget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task PercentLiteralDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = \"100%%\" % ()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PercentStrIntDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = \"%s-%d\" % (\"a\", 2)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PercentCharDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = \"%c\" % 65\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PercentReprDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = \"%r\" % 1\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task FormatDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = \"{0}{1}\".format(1, 2)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task FormatMapDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = \"{x}\".format_map({\"x\": 1})\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task FormatBehaves()
        => await AssertCompletes(
            "return \"%s-%d\" % (\"a\", 2) + \"|\" + \"{0}{1}\".format(1, 2)\n", "a-2|12");

    [Fact]
    public async Task RetainedFormatDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(\"%s-%d\" % (\"a\", 2))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(RetentionBudget);
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
