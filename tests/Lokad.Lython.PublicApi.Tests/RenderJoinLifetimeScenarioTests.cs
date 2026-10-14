using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class RenderJoinLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted(long budget) => new() { MaxExecutionMemoryBytes = budget };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task ReprListDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = repr([1, 2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ReprTupleDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = repr((1, 2))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ReprSingletonDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = repr((1,))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ReprDictDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = repr({\"a\": 1})\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ReprSetDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = repr({1, 2})\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task MultiArgExceptionDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = ValueError(1, 2)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task MultiArgExceptionRendersBehave()
    {
        var script = new LythonEngine().Compile(
            "e = ValueError(1, 2)\nreturn [str(e), repr(e)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "(1, 2)", "ValueError(1, 2)" };
        var sync = script.Run(new MockLythonHost(), Budgeted(StreamingBudget));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(StreamingBudget));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RetainedReprDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(repr([1, 2]))\n    i = i + 1\nreturn len(objs)\n");
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

    [Fact]
    public async Task FStringTupleDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = f\"{(1, 2)}\"\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task FStringIntDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = f\"{1}\"\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task FStringMixedDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = f\"abc{i}\"\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task FStringInterpolationBehaves()
        => await AssertCompletes(
            "return f\"{(1, 2)}!\"\n", "(1, 2)!");

    [Fact]
    public async Task RetainedFStringDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(f\"v{i}\")\n    i = i + 1\nreturn len(objs)\n");
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
