using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: sorted results own lifetime like list results do.
public sealed class SortedLifetimeScenarioTests
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
    public async Task SortedListDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    y = sorted([2, 1])\nreturn 0\n", "0");

    [Fact]
    public async Task SortedTupleDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    y = sorted((2, 1))\nreturn 0\n", "0");

    [Fact]
    public async Task SortedKeyDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    y = sorted([\"bb\", \"a\"], key=len)\nreturn 0\n", "0");

    [Fact]
    public async Task SortedBehaves()
        => await AssertCompletes(
            "a = sorted([3, 1, 2])\nb = sorted(\"cba\")\nc = sorted([1, 2, 3], reverse=True)\nd = sorted([\"bb\", \"a\", \"ccc\"], key=len)\nreturn str(a) + \"|\" + str(b) + \"|\" + str(c) + \"|\" + str(d)\n", "[1, 2, 3]|['a', 'b', 'c']|[3, 2, 1]|['a', 'bb', 'ccc']");

    [Fact]
    public async Task RetainedSortedDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 20000:\n    objs.append(sorted([2, 1]))\n    i = i + 1\nreturn len(objs)\n");
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