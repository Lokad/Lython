using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M05: regex factories build fresh match strings beside their containers, but
// neither ever reached a funnel, so dropped results stranded every string.
// Findall adopts items (plus nested group tuples) through the shared split
// helpers, match objects adopt their whole and capture values, and finditer
// shells charge per live instance; drops reclaim on sweep while retained
// results stay charged. Per-call pattern compilation is separate ownership.
public sealed class RegexLifetimeScenarioTests
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
    public async Task FindAllDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\",\")\nfor i in range(50000):\n    x = p.findall(\"a,b\")\nreturn 0\n", "0");

    [Fact]
    public async Task FindAllGroupsDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\"(a)(b)\")\nfor i in range(50000):\n    x = p.findall(\"ab\")\nreturn 0\n", "0");

    [Fact]
    public async Task MatchDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\"ab?\")\nfor i in range(50000):\n    x = p.match(\"abc\")\nreturn 0\n", "0");

    [Fact]
    public async Task FindIterDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\",\")\nfor i in range(50000):\n    x = list(p.finditer(\"a,b\"))\nreturn 0\n", "0");

    [Fact]
    public async Task RegexBehaves()
    {
        var script = new LythonEngine().Compile(
            "import re\np = re.compile(\"ab?\")\nm = p.match(\"abc\")\nreturn [p.findall(\"a,b\")[0], m.group(0)]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "a", "ab" };
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RetainedMatchesDenied()
    {
        var script = new LythonEngine().Compile(
            "import re\np = re.compile(\"ab?\")\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(p.match(\"abc\"))\n    i = i + 1\nreturn len(objs)\n");
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

    [Fact]
    public async Task CompileDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(20000):\n    p = re.compile(\",\")\nreturn 0\n", "0");

    [Fact]
    public async Task ModuleFindAllDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(20000):\n    x = re.findall(\",\", \"a,b\")\nreturn 0\n", "0");

    [Fact]
    public async Task ModuleSubDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(20000):\n    x = re.sub(\",\", \";\", \"a,b\")\nreturn 0\n", "0");

    [Fact]
    public async Task ModuleMatchDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(20000):\n    x = re.match(\"a\", \"abc\")\nreturn 0\n", "0");

    [Fact]
    public async Task ModuleSplitBehaves()
    {
        var script = new LythonEngine().Compile(
            "import re\nreturn re.split(\",\", \"a,b\")\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "a", "b" };
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RetainedCompilesDenied()
    {
        var script = new LythonEngine().Compile(
            "import re\nobjs = []\ni = 0\nwhile i < 200:\n    objs.append(re.compile(\"(a)(b)\"))\n    i = i + 1\nreturn len(objs)\n");
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
    [Fact]
    public async Task SubnDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(50000):\n    x = re.subn(\"a\", \"b\", \"aaa\")\nreturn 0\n", "0");

    [Fact]
    public async Task SubnCallableDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(50000):\n    x = re.subn(\"a\", lambda m: \"b\", \"aaa\")\nreturn 0\n", "0");

    [Fact]
    public async Task SubnBehaves()
    {
        var script = new LythonEngine().Compile(
            "import re\nreturn re.subn(\"a\", \"b\", \"aaa\")\n");
        Assert.True(script.IsValid);
        var expected = new object?[] { "bbb", new BigInteger(3) };
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<object[]>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<object[]>(asyncResult.ReturnValue));
    }

    [Fact]
    public async Task RetainedSubnDenied()
    {
        var script = new LythonEngine().Compile(
            "import re\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(re.subn(\"a\", \"b\", \"aaa\"))\n    i = i + 1\nreturn len(objs)\n");
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
