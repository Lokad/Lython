using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// P03: conditional expressions compile to native branches instead of the
// lowered-dispatch fallback. Guards the new block threading: continuations
// after the join, single-side evaluation, custom condition truthiness,
// nesting, and try-region safety, in both execution modes.
public sealed class ConditionalExpressionScenarioTests
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
    public async Task SelectsTakenBranchValue()
        => await AssertValue("x = 1 if 2 < 3 else 4\nreturn x\n", new BigInteger(1));

    [Fact]
    public async Task SelectsAlternativeBranchValue()
        => await AssertValue("x = 1 if 2 > 3 else 4\nreturn x\n", new BigInteger(4));

    [Fact]
    public async Task EvaluatesOnlyTakenBranch()
        => await AssertValue("seen = []\ndef mark(v):\n    seen.append(v)\n    return v\nx = mark(1) if True else mark(2)\nreturn str([x, seen])\n", "[1, [1]]");

    [Fact]
    public async Task SkipsTakenBranchSideEffects()
        => await AssertValue("seen = []\ndef mark(v):\n    seen.append(v)\n    return v\nx = mark(1) if False else mark(2)\nreturn str([x, seen])\n", "[2, [2]]");

    [Fact]
    public async Task HonorsCustomConditionTruthiness()
        => await AssertValue("class Falsy:\n    def __bool__(self):\n        return False\nclass Empty:\n    def __len__(self):\n        return 0\nreturn str([1 if Falsy() else 2, 1 if Empty() else 2, 1 if [0] else 2])\n", "[2, 2, 1]");

    [Fact]
    public async Task ThreadsContinuationsThroughCallArguments()
        => await AssertValue("def ident(v):\n    return v\nreturn ident(1 if True else 2) + ident(3 if False else 4)\n", new BigInteger(5));

    [Fact]
    public async Task NestsConditionalsInBranches()
        => await AssertValue("return (1 if True else 2) if False else (3 if True else 4)\n", new BigInteger(3));

    [Fact]
    public async Task StaysProtectedInsideTryRegion()
        => await AssertValue("try:\n    x = 1 if True else 1 // 0\n    y = 1 if False else 1 // 0\nexcept ZeroDivisionError:\n    return str([x, 0])\nreturn 99\n", "[1, 0]");

    [Fact]
    public async Task LoopReuseKeepsValue()
        => await AssertValue("x = 0\nfor i in range(100):\n    x = 1 if i < 200 else 0\nreturn x\n", new BigInteger(1));
}
