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
            "import re\nobjs = []\ni = 0\nwhile i < 200:\n    objs.append(re.compile(\"(a)(b)\" + str(i)))\n    i = i + 1\nreturn len(objs)\n");
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
    [Fact]
    public async Task GroupdictDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(50000):\n    m = re.match(\"(?P<first>a)(?P<second>b)\", \"ab\")\n    x = m.groupdict()\nreturn 0\n", "0");

    [Fact]
    public async Task GroupdictBehaves()
    {
        var script = new LythonEngine().Compile(
            "import re\nreturn re.match(\"(?P<first>a)(?P<second>b)\", \"ab\").groupdict()\n");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        var syncDict = Assert.IsType<Dictionary<object, object?>>(sync.ReturnValue);
        Assert.Equal(2, syncDict.Count);
        Assert.Equal("a", syncDict["first"]);
        Assert.Equal("b", syncDict["second"]);
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        var asyncDict = Assert.IsType<Dictionary<object, object?>>(asyncResult.ReturnValue);
        Assert.Equal(2, asyncDict.Count);
        Assert.Equal("a", asyncDict["first"]);
        Assert.Equal("b", asyncDict["second"]);
    }

    [Fact]
    public async Task RetainedGroupdictDenied()
    {
        var script = new LythonEngine().Compile(
            "import re\nobjs = []\ni = 0\nwhile i < 20000:\n    m = re.match(\"(?P<first>a)(?P<second>b)\", \"ab\")\n    objs.append(m.groupdict())\n    i = i + 1\nreturn len(objs)\n");
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
    public async Task PartialSubDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(50000):\n    x = re.sub(\"a\", \"b\", \"xaay\", 0, 0, 1, 3)\nreturn 0\n", "0");

    [Fact]
    public async Task PartialSubnDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(50000):\n    x = re.subn(\"a\", \"b\", \"xaay\", 0, 0, 1, 3)\nreturn 0\n", "0");

    [Fact]
    public async Task PartialSubCallableDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(20000):\n    x = re.sub(\"a\", lambda m: \"b\", \"xaay\", 0, 0, 1, 3)\nreturn 0\n", "0");

    [Fact]
    public async Task PartialSubBehaves()
    {
        var script = new LythonEngine().Compile(
            "import re\nreturn re.sub(\"a\", \"b\", \"xaay\", 0, 0, 1, 3)\n");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("xbby", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("xbby", asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RetainedPartialDenied()
    {
        var script = new LythonEngine().Compile(
            "import re\nobjs = []\ni = 0\nwhile i < 20000:\n    objs.append(re.sub(\"a\", \"b\", \"xaay\", 0, 0, 1, 3))\n    i = i + 1\nreturn len(objs)\n");
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
    public async Task SplitGovernedSubjectDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(50000):\n    t = \"x\" + str(i) + \"aay\"\n    x = re.split(\"a\", t, 0, 0, 1, 3)\nreturn 0\n", "0");
    [Fact]
    public async Task SearchGovernedSubjectDiscardCompletes()
        => await AssertCompletes(
            "import re\nfor i in range(50000):\n    t = \"x\" + str(i) + \"aay\"\n    x = re.search(\"a\", t, 0, 1, 3)\nreturn 0\n", "0");
    [Fact]
    public async Task CompiledSplitGovernedDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\"a\")\nfor i in range(50000):\n    t = \"x\" + str(i) + \"aay\"\n    x = p.split(t, 0, 1, 3)\nreturn 0\n", "0");
    [Fact]
    public async Task CompiledSearchGovernedDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\"a\")\nfor i in range(50000):\n    t = \"x\" + str(i) + \"aay\"\n    x = p.search(t, 1, 3)\nreturn 0\n", "0");
    [Fact]
    public async Task CompiledMatchGovernedDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\"a\")\nfor i in range(50000):\n    t = \"x\" + str(i) + \"aay\"\n    x = p.match(t, 1, 3)\nreturn 0\n", "0");
    [Fact]
    public async Task CompiledFindallGovernedDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\"a\")\nfor i in range(50000):\n    t = \"x\" + str(i) + \"aay\"\n    x = p.findall(t, 1, 3)\nreturn 0\n", "0");
    [Fact]
    public async Task CompiledSubGovernedDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\"a\")\nfor i in range(50000):\n    t = \"x\" + str(i) + \"aay\"\n    x = p.sub(\"b\", t, 0, 1, 3)\nreturn 0\n", "0");
    [Fact]
    public async Task CompiledFinditerGovernedDiscardCompletes()
        => await AssertCompletes(
            "import re\np = re.compile(\"a\")\nfor i in range(50000):\n    t = \"x\" + str(i) + \"aay\"\n    x = p.finditer(t, 1, 3)\nreturn 0\n", "0");
    [Fact]
    public async Task SplitGovernedBehaves()
    {
        var script = new LythonEngine().Compile(
            "import re\nt = \"x\" + str(7) + \"aay\"\nreturn re.split(\"a\", t, 0, 0, 1, 3)\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "7", "" };
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(sync.ReturnValue));
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, Assert.IsType<List<object?>>(asyncResult.ReturnValue));
    }
    [Fact]
    public async Task SearchGovernedBehaves()
    {
        var script = new LythonEngine().Compile(
            "import re\nt = \"x\" + str(7) + \"aay\"\nreturn re.search(\"a\", t, 0, 1, 3).group(0)\n");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("a", sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("a", asyncResult.ReturnValue);
    }
    [Fact]
    public async Task RetainedSplitGovernedDenied()
    {
        var script = new LythonEngine().Compile(
            "import re\nobjs = []\ni = 0\nwhile i < 20000:\n    t = \"x\" + str(i) + \"aay\"\n    objs.append(re.split(\"a\", t, 0, 0, 1, 3))\n    i = i + 1\nreturn len(objs)\n");
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
    public async Task RetainedSearchGovernedDenied()
    {
        var script = new LythonEngine().Compile(
            "import re\nobjs = []\ni = 0\nwhile i < 20000:\n    t = \"x\" + str(i) + \"aay\"\n    objs.append(re.search(\"a\", t, 0, 1, 3))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(OneMib / 4); // N19: shared-pattern retains fit 1MiB; match charges deny here.
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib / 4);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib / 4);
    }

    [Fact]
    public async Task SharedCompilesRetainCheaply()
    {
        // N19: retaining the same compilation shares one charged pattern instead
        // of one charge per call, so 200 shared retains fit easily where 200
        // distinct compilations deny above.
        var script = new LythonEngine().Compile(
            "import re\nobjs = []\ni = 0\nwhile i < 200:\n    objs.append(re.compile(\"(a)(b)\"))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(OneMib);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(200), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(200), asyncResult.ReturnValue);
    }

    private static long MeasureAllocated(System.Func<LythonExecutionResult> run)
    {
        var before = System.GC.GetAllocatedBytesForCurrentThread();
        var result = run();
        Assert.True(result.Success, result.Failure?.Message);
        return (long)System.GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public async Task ModuleSearchReuse_TrafficStaysNearLinear()
    {
        // N19: 1000 module-level searches of one pattern compile once (~5 MB here),
        // not once per call (~159 MB before the per-execution cache). Rearranged
        // compilation identity is CPython parity: re.compile("a") is re.compile("a").
        var script = new LythonEngine().Compile(
            "import re\nout = []\nfor i in range(1000):\n    t = \"ab\" + str(i)\n    out.append(re.search(\"a\", t, 0, 3))\nreturn [len(out), re.compile(\"a\") is re.compile(\"a\")]");
        Assert.True(script.IsValid);
        var expected = new List<object?> { new BigInteger(1000), true };
        _ = script.Run(new MockLythonHost());
        Assert.True(MeasureAllocated(() => script.Run(new MockLythonHost())) < 32000000);
        _ = await script.RunAsync(new MockLythonHost());
        Assert.True(MeasureAllocated(() => script.Run(new MockLythonHost())) < 32000000);
        var funded = script.Run(new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(expected, funded.ReturnValue);
    }

    [Fact]
    public async Task EvictedPatternsRecompileTransparently()
    {
        // N19: past the 512-entry bound the coldest patterns evict, but every
        // spelling still compiles on demand: results stay correct and the
        // surviving tail keeps its identity.
        var script = new LythonEngine().Compile(
            "import re\nfor i in range(600):\n    re.compile(\"p\" + str(i))\na = re.compile(\"p0\")\nb = re.compile(\"p599\")\nreturn [a.search(\"p0\") is not None, b.search(\"p599\") is not None, re.compile(\"p599\") is b]");
        Assert.True(script.IsValid);
        var expected = new List<object?> { true, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task PurgeRefreshesTheCache()
    {
        // N19: re.purge() drops every cached reference and releases slot charges.
        // Retained patterns keep working on their own ownership, so the next
        // same-spelling compile builds anew instead of returning the purged object.
        var script = new LythonEngine().Compile(
            "import re\np1 = re.compile(\"a\")\nre.purge()\np2 = re.compile(\"a\")\nreturn [p1 is p2, p1.search(\"xa\") is not None, p2.search(\"xa\") is not None]");
        Assert.True(script.IsValid);
        var expected = new List<object?> { false, true, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task InvalidPatternsNeverCache()
    {
        // N19: failed compilations never populate the cache: repeating an invalid
        // spelling raises PatternError every time instead of poisoning later calls.
        var script = new LythonEngine().Compile(
            "import re\nresults = []\nfor src in [\"(_\", \"(\"]:\n    try:\n        re.search(src, \"x\")\n        results.append(\"no-error\")\n    except Exception as e:\n        results.append(e.type)\nreturn results");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "PatternError", "PatternError" };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
