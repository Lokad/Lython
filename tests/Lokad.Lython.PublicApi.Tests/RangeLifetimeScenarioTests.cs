using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M05: range and iterator shells own one charge per live instance with
// reclamation through the pool once dropped.
public sealed class RangeLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;

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
    public async Task RangesDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = range(2)\nreturn 0\n", "0");

    [Fact]
    public async Task EnumerateDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    x = enumerate([1])\nreturn 0\n", "0");

    [Fact]
    public async Task MapDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    a = list(map(str, [1]))\nreturn 0\n", "0");

    [Fact]
    public async Task FilterDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    b = list(filter(None, [1]))\nreturn 0\n", "0");

    [Fact]
    public async Task ZipDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    c = list(zip([1], [2]))\nreturn 0\n", "0");

    [Fact]
    public async Task GeneratorsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(100000):\n    g = (x for x in [1])\nreturn 0\n", "0");

    [Fact]
    public async Task EnumerateConsumedDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    c = list(enumerate([1]))\nreturn 0\n", "0");

    [Fact]
    public async Task TupleZipDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    c = tuple(zip([1], [2]))\nreturn 0\n", "0");

    // Denial-mid-consumption pairing: each budget sits at a measured stable point
    // derived from roomy-budget peaks (enum 1545, zip ~2057). The script reports its own path as an
    // integer code (10 = second item denied, +index on third-item success, +100 on
    // third-item denial). N06 moved the throttle (source lists adopt 3 x 64 B
    // coupons): enum lands on late-denial completion (code 100, stable 1376-1404,
    // peak 1376) and zip on deny-then-deny (code 110, stable 1680-1720, peak 1696).
    // A recovery-aligned path (code 12/13) has no stable point left at this scale:
    // the tail margin inverts against the third-item success point. The deny-then-deny
    // path is the exhaustion-relief throttle: relief re-arms
    // only while committed keeps growing, so a drop without new retention still
    // fast-fails instead of paying a collection per caught trip. Anything else
    // (no denial, a reused index, an early denial) fails loudly.
    private static async Task AssertDenialPath(string source, long budget, params BigInteger[] accepted)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Contains(Assert.IsType<BigInteger>(sync.ReturnValue), accepted);
        var asyncResult = await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Contains(Assert.IsType<BigInteger>(asyncResult.ReturnValue), accepted);
    }

    [Fact]
    public async Task EnumerateLateDenialCompletesCleanly()
        => await AssertDenialPath(
            "it = enumerate([10, 20, 30])\ncode = 0\nt1 = next(it)\ntry:\n    t2 = next(it)\nexcept MemoryError:\n    code = code + 10\n    t2 = None\ndel t1\ntry:\n    t3 = next(it)\n    code = code + t3[0]\nexcept MemoryError:\n    code = code + 100\nreturn code\n", 1392, 100);

    [Fact]
    public async Task ZipDenialReportsRecoveryPath()
        => await AssertDenialPath(
            "it = zip([10, 20, 30], [1, 2, 3])\ncode = 0\nt1 = next(it)\ntry:\n    t2 = next(it)\nexcept MemoryError:\n    code = code + 10\n    t2 = None\ndel t1\ntry:\n    t3 = next(it)\n    code = code + t3[1]\nexcept MemoryError:\n    code = code + 100\nreturn code\n", 1696, 13, 110);

    // Shell exhaustion: 256 covers setup and the one-item source display but not the
    // pooled shell, so construction must deny cleanly instead of stranding or crashing.
    private static async Task AssertDenies(string source, long budget)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = budget });
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task EnumerateShellDeniesCleanly()
        => await AssertDenies("x = enumerate([1])\nreturn 0\n", 256);

    // Reversed/iter shells: builtin branches, iter() shapes and member dunders all own
    // one pooled shell charge per live instance. Each failed pre-fix with a stranded
    // 128B shell per iteration; set/member __iter__ sites stay a residual (explicit
    // dunder calls only, never loop iteration).
    [Fact]
    public async Task ReversedListDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = reversed([1, 2])\nreturn 0\n", "0");

    [Fact]
    public async Task ReversedRangeDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = reversed(range(3))\nreturn 0\n", "0");

    [Fact]
    public async Task ReversedStrDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = reversed(\"ab\")\nreturn 0\n", "0");

    [Fact]
    public async Task ReversedDictDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = reversed({1: 2})\nreturn 0\n", "0");

    [Fact]
    public async Task IterListDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = iter([1, 2])\nreturn 0\n", "0");

    [Fact]
    public async Task IterRangeDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = iter(range(3))\nreturn 0\n", "0");

    [Fact]
    public async Task IterStrDiscardCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = iter(\"ab\")\nreturn 0\n", "0");

    [Fact]
    public async Task ListDunderReversedCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = [1, 2].__reversed__()\nreturn 0\n", "0");

    [Fact]
    public async Task DequeDunderReversedCompletes()
        => await AssertCompletes(
            "import collections\nd = collections.deque([1, 2])\nfor i in range(50000):\n    x = d.__reversed__()\nreturn 0\n", "0");

    [Fact]
    public async Task DictDunderReversedCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(50000):\n    x = d.__reversed__()\nreturn 0\n", "0");

    [Fact]
    public async Task DictKeysDunderReversedCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(50000):\n    x = d.keys().__reversed__()\nreturn 0\n", "0");

    [Fact]
    public async Task DictValuesDunderReversedCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(50000):\n    x = d.values().__reversed__()\nreturn 0\n", "0");

    [Fact]
    public async Task DictItemsDunderReversedCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(50000):\n    x = d.items().__reversed__()\nreturn 0\n", "0");

    [Fact]
    public async Task DefaultDictDunderReversedCompletes()
        => await AssertCompletes(
            "import collections\nd = collections.defaultdict(list)\nd[\"k\"] = 1\nfor i in range(50000):\n    x = d.__reversed__()\nreturn 0\n", "0");

    [Fact]
    public async Task CounterDunderReversedCompletes()
        => await AssertCompletes(
            "import collections\nc = collections.Counter(\"ab\")\nfor i in range(50000):\n    x = c.__reversed__()\nreturn 0\n", "0");

    // Member __iter__ dunders construct the same pooled shells as iter(); explicit
    // dunder calls in a loop strand without the call-site ownership below.
    [Fact]
    public async Task ListIterDunderCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = [1, 2].__iter__()\nreturn 0\n", "0");

    [Fact]
    public async Task DictKeysIterDunderCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(50000):\n    x = d.keys().__iter__()\nreturn 0\n", "0");

    [Fact]
    public async Task StrIterDunderCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = \"ab\".__iter__()\nreturn 0\n", "0");

    [Fact]
    public async Task SetIterDunderCompletes()
        => await AssertCompletes(
            "for i in range(50000):\n    x = {1, 2}.__iter__()\nreturn 0\n", "0");

}
