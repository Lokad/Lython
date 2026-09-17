using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// P03: assignment expressions compile to duplicate-and-store instead of the
// lowered-dispatch fallback. Guards stored identity, value delivery,
// nesting, continuations and scope behavior, in both execution modes.
public sealed class AssignmentExpressionScenarioTests
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
    public async Task StoresAndDeliversValue()
        => await AssertValue("y = (x := 41 + 1)\nreturn str([x, y])\n", "[42, 42]");

    [Fact]
    public async Task NestsAssignmentsInsideOut()
        => await AssertValue("z = (x := (y := 7))\nreturn str([x, y, z])\n", "[7, 7, 7]");

    [Fact]
    public async Task ThreadsThroughCallArguments()
        => await AssertValue("def ident(v):\n    return v\nreturn ident(x := 5) + x\n", new BigInteger(10));

    [Fact]
    public async Task WorksInConditionsAndLoops()
        => await AssertValue("n = 0\nwhile (v := n) < 3:\n    n += 1\nreturn str([v, n])\n", "[3, 3]");

    [Fact]
    public async Task HonorsFunctionScope()
        => await AssertValue("x = 0\ndef f():\n    y = (x := 9)\n    return y\nreturn str([f(), x])\n", "[9, 0]");

    [Fact]
    public async Task PropagatesOutOfComprehensionScope()
        => await AssertValue("y = -1\ntotal = sum([(y := i) for i in range(4)])\nreturn str([total, y])\n", "[6, 3]");

    [Fact]
    public async Task LoopReuseKeepsStoredValue()
        => await AssertValue("x = 0\nfor i in range(100):\n    y = (x := i)\nreturn str([x, y])\n", "[99, 99]");
}
