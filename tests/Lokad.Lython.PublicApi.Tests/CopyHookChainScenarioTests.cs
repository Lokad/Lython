using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG20: alternating copy/hook chains (deepcopy -> __deepcopy__ hook ->
// deepcopy -> ...) reset the graph-depth guard per deepcopy() call, so the
// interpreter call guard bounds them instead. That guard deliberately raises
// RuntimeError (MG25), not the RecursionError used for pure-graph depth, and
// RecursionError subclasses RuntimeError, so except RuntimeError catches both
// modes. Verified lock-in: no code change needed.
public sealed class CopyHookChainScenarioTests
{
    private const string ChainPrelude =
        "import copy\n" +
        "class Node:\n" +
        "    def __deepcopy__(self, memo):\n" +
        "        c = Node()\n" +
        "        c.nxt = copy.deepcopy(self.nxt, memo)\n" +
        "        return c\n" +
        "def build(n):\n" +
        "    head = None\n" +
        "    i = 0\n" +
        "    while i < n:\n" +
        "        x = Node()\n" +
        "        x.nxt = head\n" +
        "        head = x\n" +
        "        i = i + 1\n" +
        "    return head\n" +
        "def count(head):\n" +
        "    depth = 1\n" +
        "    cur = head\n" +
        "    while cur.nxt is not None:\n" +
        "        cur = cur.nxt\n" +
        "        depth = depth + 1\n" +
        "    return depth\n";

    [Fact]
    public async Task DeepHookChainFailsInterpreterStackGuard()
    {
        var script = new LythonEngine().Compile(
            ChainPrelude +
            "m = copy.deepcopy(build(600))\n" +
            "return 0\n");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.False(sync.Success);
        Assert.Equal("RuntimeError", sync.Failure?.ExceptionType);
        Assert.Contains("maximum interpreter stack depth exceeded", sync.Failure?.Message, StringComparison.Ordinal);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.False(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("RuntimeError", asyncResult.Failure?.ExceptionType);
        Assert.Contains("maximum interpreter stack depth exceeded", asyncResult.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShallowHookChainSucceeds()
    {
        var script = new LythonEngine().Compile(
            ChainPrelude +
            "return count(copy.deepcopy(build(100)))\n");
        Assert.True(script.IsValid);
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(100), sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(100), asyncResult.ReturnValue);
    }

    [Fact]
    public async Task FailedHookChainCleansUp()
    {
        var script = new LythonEngine().Compile(
            ChainPrelude +
            "try:\n" +
            "    copy.deepcopy(build(600))\n" +
            "    outcome = 'no-raise'\n" +
            "except RuntimeError:\n" +
            "    outcome = 'raised'\n" +
            "return [outcome, count(copy.deepcopy(build(5)))]\n");
        Assert.True(script.IsValid);
        var expected = new List<object?> { "raised", new BigInteger(5) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
