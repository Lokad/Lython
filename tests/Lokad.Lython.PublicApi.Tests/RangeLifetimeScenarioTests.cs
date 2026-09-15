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

    // Denial-mid-consumption pairing: each budget sits between first-item success
    // (fixed charges + 192) and second-item reservation (fixed + 384), derived from
    // roomy-budget peaks (enum 1216, zip 1536). The script reports its own path as an
    // integer code (10 = second item denied, +index on third-item success, +100 on
    // third-item denial); accepted paths are deny-then-recover-aligned and
    // deny-then-deny. The latter is the exhaustion-relief throttle: relief re-arms
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
    public async Task EnumerateDenialPreservesIndexAlignment()
        => await AssertDenialPath(
            "it = enumerate([10, 20, 30])\ncode = 0\nt1 = next(it)\ntry:\n    t2 = next(it)\nexcept MemoryError:\n    code = code + 10\n    t2 = None\ndel t1\ntry:\n    t3 = next(it)\n    code = code + t3[0]\nexcept MemoryError:\n    code = code + 100\nreturn code\n", 928, 12, 110);

    [Fact]
    public async Task ZipDenialReportsRecoveryPath()
        => await AssertDenialPath(
            "it = zip([10, 20, 30], [1, 2, 3])\ncode = 0\nt1 = next(it)\ntry:\n    t2 = next(it)\nexcept MemoryError:\n    code = code + 10\n    t2 = None\ndel t1\ntry:\n    t3 = next(it)\n    code = code + t3[1]\nexcept MemoryError:\n    code = code + 100\nreturn code\n", 1248, 13, 110);

}
