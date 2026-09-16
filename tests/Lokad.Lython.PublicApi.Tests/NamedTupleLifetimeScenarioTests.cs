using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: namedtuple construction results own lifetime like the other factory results do.
public sealed class NamedTupleLifetimeScenarioTests
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
    public async Task NamedTupleCtorDiscardCompletes()
        => await AssertCompletes(
            "from collections import namedtuple\nP = namedtuple('P', ['x', 'y'])\nfor i in range(100000):\n    p = P(1, 2)\nreturn 0\n", "0");

    [Fact]
    public async Task NamedTupleMakeDiscardCompletes()
        => await AssertCompletes(
            "from collections import namedtuple\nP = namedtuple('P', ['x', 'y'])\nfor i in range(100000):\n    p = P._make([1, 2])\nreturn 0\n", "0");

    [Fact]
    public async Task NamedTupleReplaceDiscardCompletes()
        => await AssertCompletes(
            "from collections import namedtuple\nP = namedtuple('P', ['x', 'y'])\np = P(1, 2)\nfor i in range(100000):\n    q = p._replace(x=3)\nreturn 0\n", "0");

    [Fact]
    public async Task NamedTupleAsDictDiscardCompletes()
        => await AssertCompletes(
            "from collections import namedtuple\nP = namedtuple('P', ['x', 'y'])\np = P(1, 2)\nfor i in range(100000):\n    d = p._asdict()\nreturn 0\n", "0");

    [Fact]
    public async Task NamedTupleFreshBehaves()
        => await AssertCompletes(
            "from collections import namedtuple\nP = namedtuple('P', ['x', 'y'])\np = P(1, 2)\nreturn str(P._make([3, 4])) + str(p._replace(x=5)) + str(p._asdict())\n", "P(x=3, y=4)P(x=5, y=2){'x': 1, 'y': 2}");

    [Fact]
    public async Task RetainedNamedTupleDenied()
    {
        var script = new LythonEngine().Compile(
            "from collections import namedtuple\nP = namedtuple('P', ['x', 'y'])\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(P(1, 2))\n    i = i + 1\nreturn len(objs)\n");
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
