using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: fresh ChainMap maps own lifetime like the other factory results do.
public sealed class ChainMapLifetimeScenarioTests
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
    public async Task ChainMapEmptyDiscardCompletes()
        => await AssertCompletes(
            "from collections import ChainMap\nfor i in range(50000):\n    x = ChainMap()\nreturn 0\n", "0");

    [Fact]
    public async Task ChainMapDefaultdictArgDiscardCompletes()
        => await AssertCompletes(
            "from collections import ChainMap, defaultdict\nfor i in range(50000):\n    x = ChainMap(defaultdict(int, {'a': 1}))\nreturn 0\n", "0");

    [Fact]
    public async Task ChainMapCopyDiscardCompletes()
        => await AssertCompletes(
            "from collections import ChainMap\nm = ChainMap({'a': 1})\nfor i in range(50000):\n    x = m.copy()\nreturn 0\n", "0");

    [Fact]
    public async Task ChainMapOrMapDiscardCompletes()
        => await AssertCompletes(
            "from collections import ChainMap\nm = ChainMap({'a': 1})\nfor i in range(50000):\n    x = m | {'b': 2}\nreturn 0\n", "0");

    [Fact]
    public async Task ChainMapOrChainMapDiscardCompletes()
        => await AssertCompletes(
            "from collections import ChainMap\na = ChainMap({'a': 1})\nb = ChainMap({'b': 2})\nfor i in range(50000):\n    x = a | b\nreturn 0\n", "0");

    [Fact]
    public async Task ChainMapNewChildDefaultdictDiscardCompletes()
        => await AssertCompletes(
            "from collections import ChainMap, defaultdict\nm = ChainMap({'a': 1})\nfor i in range(50000):\n    x = m.new_child(defaultdict(int, {'b': 2}))\nreturn 0\n", "0");

    [Fact]
    public async Task ChainMapFreshBehaves()
        => await AssertCompletes(
            "from collections import ChainMap\nm = ChainMap({'a': 1})\nc = m.copy()\nu = m | {'b': 2}\ne = ChainMap()\nreturn str([c['a'], u['a'], u['b'], len(e.maps)])\n", "[1, 1, 2, 1]");

    [Fact]
    public async Task RetainedChainMapCopyDenied()
    {
        var script = new LythonEngine().Compile(
            "from collections import ChainMap\nm = ChainMap({'a': 1})\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(m.copy())\n    i = i + 1\nreturn len(objs)\n");
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
