using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// P03: chained comparisons compile to native links instead of the
// lowered-dispatch fallback. Guards the retained-left slot threading:
// link positions, mixed operators, single evaluation, short-circuit
// isolation, nesting, and region safety, in both execution modes.
public sealed class ChainedComparisonScenarioTests
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
    public async Task FailsAtEachLinkPosition()
        => await AssertValue("return str([0 < 1 < 2, 0 < 9 < 2, 9 < 1 < 2, 0 < 1 < 2 < 3, 0 < 1 < 9 < 3])\n", "[True, False, False, True, False]");

    [Fact]
    public async Task ThreadsPreviousRightAsNextLeft()
        => await AssertValue("return str([1 < 2 == True, 1 in (1, 2) == True, 3 is 3 is True])\n", "[False, False, False]");

    [Fact]
    public async Task MixesMembershipAndIdentityLinks()
        => await AssertValue("t = (1, 2)\nreturn str([1 in t != False, 1 < 2 is True, 0 < 1 in t])\n", "[True, False, True]");

    [Fact]
    public async Task EvaluatesEachOperandOnce()
        => await AssertValue("seen = []\ndef mid(v):\n    seen.append(v)\n    return v\nr = 0 < mid(1) < mid(2) < 3\nreturn str([r, seen])\n", "[True, [1, 2]]");

    [Fact]
    public async Task SkipsLaterOperandsAfterFailure()
        => await AssertValue("seen = []\ndef mid(v):\n    seen.append(v)\n    return v\nr = 0 < mid(1) < 0 < mid(2)\nreturn str([r, seen])\n", "[False, [1]]");

    [Fact]
    public async Task HonorsCustomComparisonDispatch()
        => await AssertValue("class Box:\n    def __init__(self, value):\n        self.value = value\n    def __lt__(self, other):\n        return self.value < other.value\nreturn str([Box(1) < Box(2) < Box(3), Box(1) < Box(9) < Box(3)])\n", "[True, False]");

    [Fact]
    public async Task ThreadsThroughCallArguments()
        => await AssertValue("def ident(v):\n    return v\nreturn ident(0 < 1 < 2) and ident(2 > 3 or False)\n", false);

    [Fact]
    public async Task StaysProtectedInsideTryRegion()
        => await AssertValue("try:\n    x = 0 < 1 // 0 < 2\nexcept ZeroDivisionError:\n    x = 99\nreturn x\n", new BigInteger(99));

    [Fact]
    public async Task LoopReuseKeepsValue()
        => await AssertValue("n = 0\nfor i in range(100):\n    n += 0 < i < 200\nreturn n\n", new BigInteger(99));
}
