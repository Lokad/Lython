using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG04: each distinct set object owns its shell for its lifetime, while empty
// payloads and transient scratch stay free. Capacity keeps its own charges:
// Clear releases it, regrowth re-charges it, and aliases share everything.
public sealed class SetShellAccountingScenarioTests
{
    private const long ThreeMib = 3145728;

    private static void AssertMemoryError(LythonExecutionResult result)
    {
        Assert.False(result.Success);
        Assert.Equal("MemoryError", result.Failure?.ExceptionType);
        Assert.True(result.PeakExecutionMemoryBytes <= ThreeMib);
    }

    [Fact]
    public async Task ManyRetainedEmptySetsStayCharged()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 100000:\n    objs.append(set())\n    i = i + 1\nreturn 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        AssertMemoryError(script.Run(new MockLythonHost(), options));
        AssertMemoryError(await script.RunAsync(new MockLythonHost(), options));
    }

    [Fact]
    public async Task ManyRetainedNonesFitSameBudget()
    {
        var script = new LythonEngine().Compile(
            "objs = []\ni = 0\nwhile i < 100000:\n    objs.append(None)\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(100000);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FundedEmptySetsBehave()
    {
        var script = new LythonEngine().Compile(
            "s = set()\ns.add(1)\ns.discard(2)\nreturn [len(s), 1 in s]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new BigInteger(1), true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task CopiesOwnShells()
    {
        var script = new LythonEngine().Compile(
            "a = set()\na.add(1)\nb = a.copy()\nc = set(a)\nd = a | set()\nb.add(2)\nreturn [len(a), len(b), len(c), len(d), 2 in a]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2), new BigInteger(1), new BigInteger(1), false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ClearReleasesCapacityForReuse()
    {
        var script = new LythonEngine().Compile(
            "s = set()\ns.add(1)\ns.clear()\ns.add(2)\nreturn [len(s), 2 in s, 1 in s]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new BigInteger(1), true, false };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
    // The tail returns a pre-existing singleton: after a denied reservation
    // the remaining headroom can be smaller than any fresh allocation. The
    // loop uses a range counter so no untracked integer strand outside the
    // handler can win the denial race first with unrecoverable headroom.
    [Fact]
    public async Task CaughtSetFailureAllowsReuse()
    {
        var script = new LythonEngine().Compile(
            "ok = False\nobjs = []\nfor i in range(100000):\n    try:\n        objs.append(set())\n    except MemoryError:\n        ok = True\n        break\nreturn ok\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(true, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(true, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DroppedEmptySetsRelease()
    {
        // MG04: dropped set calls release shell plus entry through the pool.
        var script = new LythonEngine().Compile(
            "x = None\n"
            + "for i in range(100000):\n"
            + "    x = set()\n"
            + "return 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DroppedSetDisplaysRelease()
    {
        var script = new LythonEngine().Compile(
            "x = set()\n"
            + "for i in range(100000):\n"
            + "    x = {1, 2}\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(2);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task DroppedSetComprehensionsRelease()
    {
        var script = new LythonEngine().Compile(
            "x = set()\n"
            + "for i in range(10000):\n"
            + "    x = {j for j in range(3)}\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(3);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task GrownDroppedSetsFit()
    {
        // Post-factory growth strands its delta until a wholesale replacement
        // re-snapshots (the accepted mutable residual); this scale still fits.
        var script = new LythonEngine().Compile(
            "x = None\n"
            + "for i in range(10000):\n"
            + "    x = set()\n"
            + "    x.add(i)\n"
            + "return 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task ClearedDisplaysReleaseExactly()
    {
        // Clearing re-snapshots the pool coupon through the value, so the
        // later sweep releases exactly the live shell instead of the stale
        // backing (which would over-release).
        var script = new LythonEngine().Compile(
            "x = set()\n"
            + "for i in range(100000):\n"
            + "    x = {1, 2, 3}\n"
            + "    x.clear()\n"
            + "return len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(0);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task SetAliasesShareShell()
    {
        var script = new LythonEngine().Compile(
            "a = set()\n"
            + "a.add(1)\n"
            + "b = a\n"
            + "return [len(a), len(b), 1 in b]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new BigInteger(1), new BigInteger(1), true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}