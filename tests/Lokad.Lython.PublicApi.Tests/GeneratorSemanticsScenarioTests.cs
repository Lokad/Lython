using System.Numerics;
using System.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// S01: generator expressions share one iteration scope per run, like eager
// comprehensions: loop targets rebind the same cells (closures observe final
// values), assignment expressions escape to the defining scope on each advance
// (partial consumption already binds), and filter clauses use contextual
// truthiness. Eager-closure guards pin the consistency.
public sealed class GeneratorSemanticsScenarioTests
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
    public async Task CollectedLambdasShareFinalBinding()
        => await AssertValue("funcs = list((lambda: i) for i in range(3))\nreturn [f() for f in funcs]\n", new List<object?> { new BigInteger(2), new BigInteger(2), new BigInteger(2) });

    [Fact]
    public async Task NestedLoopLambdasShareFinalPair()
        => await AssertValue("funcs = list((lambda: i * 10 + j) for i in range(2) for j in range(2))\nreturn [f() for f in funcs]\n", new List<object?> { new BigInteger(11), new BigInteger(11), new BigInteger(11), new BigInteger(11) });

    [Fact]
    public async Task WalrusEscapesToDefiningScope()
        => await AssertValue("y = -1\nvals = list((y := i) for i in range(3))\nreturn y\n", new BigInteger(2));

    [Fact]
    public async Task PartialAdvanceBindsWalrusImmediately()
        => await AssertValue("y = -1\ng = ((y := i) for i in range(3))\nfirst = next(g)\nreturn first * 10 + y\n", new BigInteger(0));

    [Fact]
    public async Task EscapedClosureSeesLatestBinding()
        => await AssertValue("g = ((lambda: i) for i in range(3))\nfirst = next(g)\nbefore = first()\nlist(g)\nafter = first()\nreturn before * 100 + after\n", new BigInteger(2));

    [Fact]
    public async Task CustomBoolFiltersItems()
        => await AssertValue("class Falsy:\n    def __bool__(self):\n        return False\nreturn list(x for x in [1, 2] if Falsy())\n", new List<object?>());

    [Fact]
    public async Task CustomLenFiltersItems()
        => await AssertValue("class Empty:\n    def __len__(self):\n        return 0\nreturn list(x for x in [1, 2] if Empty())\n", new List<object?>());

    [Fact]
    public async Task MultiClauseWithCondition()
        => await AssertValue("return list(i * 10 + j for i in range(2) for j in range(2) if (i + j) % 2 == 0)\n", new List<object?> { new BigInteger(0), new BigInteger(11) });

    [Fact]
    public async Task PredicateExceptionPropagates()
        => await AssertValue("def f(x):\n    if x == 2:\n        raise ValueError(\"boom\")\n    return True\ntry:\n    result = list(x for x in [1, 2, 3] if f(x))\n    return -1\nexcept ValueError:\n    return 1\n", new BigInteger(1));

    [Fact]
    public async Task EagerClosureSharesFinalBinding()
        => await AssertValue("funcs = [(lambda: i) for i in range(3)]\nreturn [f() for f in funcs]\n", new List<object?> { new BigInteger(2), new BigInteger(2), new BigInteger(2) });

    [Fact]
    public async Task EagerWalrusEscapesToDefiningScope()
        => await AssertValue("y = -1\nvals = [(y := i) for i in range(3)]\nreturn y\n", new BigInteger(2));

    [Fact]
    public void ConsumingGeneratorHonorsCancellation()
    {
        // The host hook cancels synchronously on the run thread inside the third
        // print; the engine must observe it while advancing the generator.
        using var cts = new CancellationTokenSource();
        var host = new MockLythonHost();
        var writes = 0;
        host.OnStandardOutputWrite = () =>
        {
            if (++writes == 3)
            {
                cts.Cancel();
            }
        };
        var result = new LythonEngine().Run(
            "g = (print(\"item-\" + str(i)) or i for i in range(30))\nreturn list(g)\n",
            host,
            new LythonRunOptions { CancellationToken = cts.Token });
        Assert.False(result.Success);
        Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
        Assert.Contains("canceled", result.Failure?.Message, StringComparison.Ordinal);
    }


    [Fact]
    public async Task DelayedAsyncIterationSuspendsMidConsumption()
    {
        // Each item opens and reads a host file, so every advance suspends on
        // the delayed host; the shared iteration scope (and the walrus below) must
        // survive those suspensions. File-backed outer iterables in async mode stay
        // a separate deferred item (async outer acquisition).
        var host = new DelayedLythonHost();
        host.SeedFile("/d.txt", "a,b\n");
        var script = new LythonEngine().Compile(
            "total = 0\nfor x in (last := open(\"/d.txt\").read() for i in range(5)):\n    total += 1\nreturn total + len(last)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var asyncResult = await script.RunAsync(host, new LythonRunOptions());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(9), asyncResult.ReturnValue);
        Assert.True(host.CompletedAsynchronously > 0);
    }
}
