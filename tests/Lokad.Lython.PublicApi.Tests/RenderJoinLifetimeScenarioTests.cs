using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M05: join builders byte-copy each rendered item, so nested item transients
// are pool-owned at the join and dropped renders reclaim on sweep instead of
// stranding 128+len B per item. Covers repr joins and multi-arg exception
// messages (which render through the tuple repr join).
public sealed class RenderJoinLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private static LythonRunOptions Budgeted(long budget) => new() { MaxExecutionMemoryBytes = budget };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task ReprListDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = repr([1, 2])\nreturn 0\n", "0");

    [Fact]
    public async Task ReprTupleDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = repr((1, 2))\nreturn 0\n", "0");

    [Fact]
    public async Task ReprSingletonDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = repr((1,))\nreturn 0\n", "0");

    [Fact]
    public async Task ReprDictDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = repr({\"a\": 1})\nreturn 0\n", "0");

    [Fact]
    public async Task ReprSetDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = repr({1, 2})\nreturn 0\n", "0");

    [Fact]
    public async Task MultiArgExceptionDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = ValueError(1, 2)\nreturn 0\n", "0");

    [Fact]
    public async Task MultiArgExceptionRendersBehave()
    {
        var script = new LythonEngine().Compile(
            "e = ValueError(1, 2)\nreturn [str(e), repr(e)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "(1, 2)", "ValueError(1, 2)" };
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RetainedReprDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 20000:\n    objs.append(repr([1, 2]))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(OneMib);
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
