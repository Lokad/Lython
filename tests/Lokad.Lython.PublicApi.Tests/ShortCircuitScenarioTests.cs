using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// P03: and/or branch natively instead of the lowered-dispatch fallback.
// Guards the short-circuit shapes: operand (not bool) results, single-side
// evaluation, chaining, custom left truthiness, continuations, and region
// safety, in both execution modes.
public sealed class ShortCircuitScenarioTests
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
    public async Task ReturnsOperandValues()
        => await AssertValue("return str([0 and 1, 2 and 3, 0 or 1, 2 or 3, \"\" or \"d\", [] and 1])\n", "[0, 3, 1, 2, \'d\', []]");

    [Fact]
    public async Task SkipsRightSideWhenDecided()
        => await AssertValue("seen = []\ndef mark(v):\n    seen.append(v)\n    return v\nx = mark(0) and mark(1)\ny = mark(2) or mark(3)\nreturn str([x, y, seen])\n", "[0, 2, [0, 2]]");

    [Fact]
    public async Task EvaluatesRightSideWhenNeeded()
        => await AssertValue("seen = []\ndef mark(v):\n    seen.append(v)\n    return v\nx = mark(1) and mark(2)\ny = mark(0) or mark(3)\nreturn str([x, y, seen])\n", "[2, 3, [1, 2, 0, 3]]");

    [Fact]
    public async Task ChainsLeftAssociatively()
        => await AssertValue("return str([1 and 2 and 3, 0 or 0 or 5, 0 and 1 or 2, 1 or 0 and 9])\n", "[3, 5, 2, 1]");

    [Fact]
    public async Task HonorsCustomLeftTruthiness()
        => await AssertValue("class Falsy:\n    def __bool__(self):\n        return False\nb = Falsy()\nreturn str([(b or 7) == 7, (b and 7) is b, [1] and 8])\n", "[True, True, 8]");

    [Fact]
    public async Task ThreadsThroughCallArguments()
        => await AssertValue("def ident(v):\n    return v\nreturn ident(0 or 1) + ident(2 and 3)\n", new BigInteger(4));

    [Fact]
    public async Task FalsyLeftOrKeepsStackBalancedInLoops()
        => await AssertValue("n = 0\nfor i in range(100):\n    n += (i or 7) - (i and 0)\nreturn n\n", new BigInteger(4957));

    [Fact]
    public async Task StaysProtectedInsideTryRegion()
        => await AssertValue("try:\n    x = 0 or 1 // 0\nexcept ZeroDivisionError:\n    x = 99\nreturn x\n", new BigInteger(99));

    [Fact]
    public async Task NegatesGroupedOperators()
        => await AssertValue("return str([not (0 or 0), not (1 and 2), not 0 and 0])\n", "[True, False, 0]");
}
