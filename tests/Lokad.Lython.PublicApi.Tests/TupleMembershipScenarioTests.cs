using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// P03: tuple membership scans by index (no per-check enumerator box).
// Guards the indexed scan against the old enumerating path: positions,
// misses, empty and mixed-type tuples, identity fast path, and negation,
// in both execution modes. Member __eq__ dispatch inside containment is
// covered by ContainmentProtocolScenarioTests; default equality still
// resolves by identity here.
public sealed class TupleMembershipScenarioTests
{
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
    public async Task FindsFirstMiddleAndLastItems()
        => await AssertValue("t = (10, 20, 30)\nreturn str([10 in t, 20 in t, 30 in t])\n", "[True, True, True]");

    [Fact]
    public async Task MissesAbsentItems()
        => await AssertValue("t = (10, 20, 30)\nreturn str([40 in t, 10 not in t])\n", "[False, False]");

    [Fact]
    public async Task EmptyTupleContainsNothing()
        => await AssertValue("t = ()\nreturn str([1 in t, 1 not in t])\n", "[False, True]");

    [Fact]
    public async Task MixedTypeTupleComparesByValue()
        => await AssertValue("t = (1, \"a\", (2, 3))\nreturn str([1 in t, \"a\" in t, (2, 3) in t, 2 in t])\n", "[True, True, True, False]");

    [Fact]
    public async Task IdenticalMemberIsFound()
        => await AssertValue("class Box:\n    pass\nb = Box()\nt = (b, Box())\nreturn str([b in t, Box() in t])\n", "[True, False]");

    [Fact]
    public async Task HoistedTupleReusedAcrossIterations()
        => await AssertValue("t = (1, 2)\nn = 0\nfor i in range(100):\n    n += 1 if i in t else 0\nreturn n\n", new BigInteger(2));
}
