using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: dict.update kwargs keys own lifetime like the dict-ctor kwargs keys do.
public class DictUpdateLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = StreamingBudget };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task UpdateKwargsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    d = {}\n    d.update(a=1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task UpdateKwargsDroppedReceiverDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    {}.update(a=1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task UpdateMappingDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    d = {}\n    d.update({'a': 1})\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task UpdateBehaves()
        => await AssertCompletes(
            "d = {}\nd.update(a=1, b=2)\nd.update({'c': 3})\nreturn str(d)\n", "{'a': 1, 'b': 2, 'c': 3}");

    [Fact]
    public async Task RetainedUpdateDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    d = {}\n    d.update(a=1)\n    objs.append(d)\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget };
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