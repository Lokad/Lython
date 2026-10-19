using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// Nested definitions and closure metadata stay charged to their execution batch.
public class NestedFunctionLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task NestedDefDiscardCompletes()
        => await AssertCompletes(
            "def outer():\n    def inner():\n        return 1\n    return inner\nfor i in range(" + BatchIterations(50000) + "):\n    y = outer()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task NestedDefCapturingDiscardCompletes()
        => await AssertCompletes(
            "def outer():\n    x = 1\n    def inner():\n        return x\n    return inner\nfor i in range(" + BatchIterations(50000) + "):\n    y = outer()()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ModuleLoopDefDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    def f():\n        return 1\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task NestedDefBehaves()
        => await AssertCompletes(
            "def outer(v):\n    x = 1\n    def inner():\n        return v * 2 + x\n    return inner\nreturn str(outer(21)())\n", "43");

    [Fact]
    public async Task DocumentedNestedDefDiscardCompletes()
        => await AssertCompletes(
            "def outer():\n    def inner():\n        'docs'\n        return 1\n    return inner\nfor i in range(" + BatchIterations(20000) + "):\n    y = outer()\nreturn 0\n", "0", Iterations(20000));

    [Fact]
    public async Task DocumentedNestedDefBehaves()
        => await AssertCompletes(
            "def f():\n    'hello'\n    return 1\nreturn f.__doc__\n", "hello");

    [Fact]
    public async Task RetainedNestedDefDenied()
    {
        var script = new LythonEngine().Compile(
            "def outer():\n    def inner():\n        return 1\n    return inner\nobjs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(outer())\n    i = i + 1\nreturn len(objs)\n");
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
