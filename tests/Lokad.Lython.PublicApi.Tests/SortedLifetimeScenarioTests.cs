using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: sorted results own lifetime like list results do.
public class SortedLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task SortedListDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    y = sorted([2, 1])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task SortedTupleDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    y = sorted((2, 1))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task SortedKeyDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    y = sorted([\"bb\", \"a\"], key=len)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task SortedBehaves()
        => await AssertCompletes(
            "a = sorted([3, 1, 2])\nb = sorted(\"cba\")\nc = sorted([1, 2, 3], reverse=True)\nd = sorted([\"bb\", \"a\", \"ccc\"], key=len)\nreturn str(a) + \"|\" + str(b) + \"|\" + str(c) + \"|\" + str(d)\n", "[1, 2, 3]|['a', 'b', 'c']|[3, 2, 1]|['a', 'bb', 'ccc']");

    [Fact]
    public async Task RetainedSortedDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(sorted([2, 1]))\n    i = i + 1\nreturn len(objs)\n");
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