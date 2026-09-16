using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M05: split factories build fresh item strings beside their container, but
// only the container ever reached a funnel, so dropped results stranded every
// item. Member sites adopt every governed string item plus the fresh container
// (partition additionally threads the call governor so literal-receiver slices
// stay governed); drops reclaim on sweep while retained results stay charged.
public sealed class SplitLifetimeScenarioTests
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
    public async Task StrSplitDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = \"a,b,c\".split(\",\")\nreturn 0\n", "0");

    [Fact]
    public async Task StrRsplitDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = \"a,b\".rsplit(\",\")\nreturn 0\n", "0");

    [Fact]
    public async Task StrSplitlinesDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = \"a\\nb\".splitlines()\nreturn 0\n", "0");

    [Fact]
    public async Task GovernedSplitDiscardCompletes()
        => await AssertCompletes(
            "s = \"a\" + \"b,c\"\nfor i in range(50000):\n    x = s.split(\",\")\nreturn 0\n", "0");

    [Fact]
    public async Task PartitionDiscardCompletes()
        => await AssertCompletes(
            "s = \"a\" + \"b,c\"\nfor i in range(50000):\n    x = s.partition(\",\")\nreturn 0\n", "0");

    [Fact]
    public async Task LiteralPartitionDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = \"a,b\".partition(\",\")\nreturn 0\n", "0");

    [Fact]
    public async Task RegexSplitDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\",\")\nfor i in range(50000):\n    x = p.split(\"a,b\")\nreturn 0\n", "0");

    [Fact]
    public async Task ShlexSplitDiscardCompletes()
        => await AssertCompletes(
            "import shlex\nfor i in range(50000):\n    x = shlex.split(\"a b\")\nreturn 0\n", "0");

    [Fact]
    public async Task SplitBehaves()
    {
        var script = new LythonEngine().Compile(
            "return [\"a,b,c\".split(\",\")[1], \"a,b\".partition(\",\")[2]]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "b", "b" };
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task RetainedSplitDenied()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 20000:\n    objs.append(\"a,b,c\".split(\",\"))\n    i = i + 1\nreturn len(objs)\n");
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
