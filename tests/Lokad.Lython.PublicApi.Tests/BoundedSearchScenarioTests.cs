using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// H02: bounded find/rfind/count/startswith/endswith compare over the UTF-8
// span instead of materializing a governed slice per call, so bounded scans
// allocate linearly. Covers the discard lifetime, the positional-scan lead
// shape, and unicode/empty bounds, in both execution modes.
public sealed class BoundedSearchScenarioTests
{
    private const long ThreeMib = 3145728;

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

    private static async Task AssertValue(string source, object expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), new LythonRunOptions());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task BoundedStartswithDiscardCompletes()
        => await AssertCompletes(
            "s = \"a\" * 100\nfor i in range(50000):\n    x = s.startswith(\"a\", 10, 90)\nreturn 0\n", "0");

    [Fact]
    public async Task PositionalScanBehaves()
        => await AssertValue(
            "s = \"xxshow markdown yy\" + \"show markdown\"\nreturn str([i for i in range(len(s)) if s.startswith(\"show markdown\", i)])\n",
            "[2, 18]");

    [Fact]
    public async Task BoundedSearchUnicodeBehaves()
        => await AssertValue(
            "s = \"aé😀b\"\nreturn str([s.find(\"😀\", 1, 3), s.rfind(\"a\", 0, 4), s.count(\"é\", 0, 4), s.startswith(\"é\", 1, 3), s.endswith(\"é\", 0, 2), s.find(\"\", 2, 2)])\n",
            "[2, 0, 1, True, True, 2]");
}
