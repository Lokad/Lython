using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: fresh path shells own lifetime like the other factory results do.
public sealed class PathLifetimeScenarioTests
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
    public async Task PathCtorDiscardCompletes()
        => await AssertCompletes(
            "from pathlib import Path\nfor i in range(100000):\n    x = Path('/a/b')\nreturn 0\n", "0");

    [Fact]
    public async Task PathJoinDiscardCompletes()
        => await AssertCompletes(
            "from pathlib import Path\nfor i in range(100000):\n    x = Path('/a') / 'b'\nreturn 0\n", "0");

    [Fact]
    public async Task PathJoinpathDiscardCompletes()
        => await AssertCompletes(
            "from pathlib import Path\np = Path('/a/b/c.txt')\nfor i in range(100000):\n    x = p.joinpath('d')\nreturn 0\n", "0");

    [Fact]
    public async Task PathParentsDiscardCompletes()
        => await AssertCompletes(
            "from pathlib import Path\nfor i in range(50000):\n    x = Path('/a/b/c/d/e/f/g').parents\nreturn 0\n", "0");

    [Fact]
    public async Task PathPartsDiscardCompletes()
        => await AssertCompletes(
            "from pathlib import Path\nfor i in range(50000):\n    x = Path('/a/b/c/d/e/f/g').parts\nreturn 0\n", "0");

    [Fact]
    public async Task PathSuffixesDiscardCompletes()
        => await AssertCompletes(
            "from pathlib import Path\nfor i in range(50000):\n    x = Path('/a/b.tar.gz.bz2.zip').suffixes\nreturn 0\n", "0");

    [Fact]
    public async Task PathPluralBehaves()
        => await AssertCompletes(
            "from pathlib import Path\np = Path('/a/b/c.tar.gz')\nreturn str(p.parent) + str(p.suffixes) + str(p.parts[2])\n", "/a/b['.tar', '.gz']b");

    [Fact]
    public async Task RetainedPathParentsDenied()
    {
        var script = new LythonEngine().Compile(
            "from pathlib import Path\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(Path('/a/b/c/d/e/f/g').parents)\n    i = i + 1\nreturn len(objs)\n");
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
    [Fact]
    public async Task PathFreshBehaves()
        => await AssertCompletes(
            "from pathlib import Path\np = Path('/a/b')\nreturn str(p / 'c') + str(p.joinpath('d', 'e')) + Path('/x').name\n", "/a/b/c/a/b/d/ex");

    [Fact]
    public async Task RetainedPathJoinDenied()
    {
        var script = new LythonEngine().Compile(
            "from pathlib import Path\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(Path('/a') / 'b')\n    i = i + 1\nreturn len(objs)\n");
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
