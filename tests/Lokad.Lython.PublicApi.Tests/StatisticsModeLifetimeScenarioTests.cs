using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: mode/multimode frequency scratch owns lifetime like other drains do.
public sealed class StatisticsModeLifetimeScenarioTests
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
    public async Task MultimodeDiscardCompletes()
        => await AssertCompletes(
            "import statistics\nfor i in range(20000):\n    y = statistics.multimode([1, 1, 2])\nreturn 0\n", "0");

    [Fact]
    public async Task ModeDiscardCompletes()
        => await AssertCompletes(
            "import statistics\nfor i in range(20000):\n    y = statistics.mode([1, 1, 2])\nreturn 0\n", "0");

    [Fact]
    public async Task MultimodeBehaves()
        => await AssertCompletes(
            "import statistics\nreturn str(statistics.multimode([1, 1, 2, 3, 3])) + \"|\" + str(statistics.mode([1, 1, 2, 3, 3]))\n", "[1, 3]|1");

    [Fact]
    public async Task RetainedMultimodeDenied()
    {
        var script = new LythonEngine().Compile(
            "import statistics\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(statistics.multimode([1, 1, 2]))\n    i = i + 1\nreturn len(objs)\n");
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