using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: mode/multimode frequency scratch owns lifetime like other drains do.
public class StatisticsModeLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task MultimodeDiscardCompletes()
        => await AssertCompletes(
            "import statistics\nfor i in range(" + BatchIterations(20000) + "):\n    y = statistics.multimode([1, 1, 2])\nreturn 0\n", "0", Iterations(20000));

    [Fact]
    public async Task ModeDiscardCompletes()
        => await AssertCompletes(
            "import statistics\nfor i in range(" + BatchIterations(20000) + "):\n    y = statistics.mode([1, 1, 2])\nreturn 0\n", "0", Iterations(20000));

    [Fact]
    public async Task MultimodeBehaves()
        => await AssertCompletes(
            "import statistics\nreturn str(statistics.multimode([1, 1, 2, 3, 3])) + \"|\" + str(statistics.mode([1, 1, 2, 3, 3]))\n", "[1, 3]|1");

    [Fact]
    public async Task RetainedMultimodeDenied()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import statistics\nobjs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(statistics.multimode([1, 1, 2]))\n    i = i + 1\nreturn len(objs)\n");
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