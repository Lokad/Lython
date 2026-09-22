using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N08: functools cache accounting is an ownership transaction with explicit
// key construction/insertion/publication (plus N12 contextual key equality).
public sealed class FunctoolsCacheOwnershipTests
{
    private static LythonRunOptions Tiny() => new()
    {
        MaxExecutionMemoryBytes = 262144,
    };

    private static async Task AssertBothModes(string source, string expectedOutput)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expectedOutput, sync.StandardOutput);
        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expectedOutput, asyncResult.StandardOutput);
    }

    [Fact]
    public async Task ChurnBoundedCacheSucceeds()
        => await AssertBothModes(
            "import functools\n@functools.lru_cache(maxsize=1)\ndef f(x):\n    return x\nfor i in range(10000):\n    f(i)\nprint(f.cache_info())\n",
            "CacheInfo(hits=0, misses=10000, maxsize=1, currsize=1)\n");

    [Fact]
    public async Task ClearEachInsertionSucceeds()
        => await AssertBothModes(
            "import functools\n@functools.cache\ndef f(x):\n    return x\nfor i in range(1000):\n    f(i)\n    f.cache_clear()\nprint(f.cache_info())\n",
            "CacheInfo(hits=0, misses=0, maxsize=None, currsize=0)\n");

    [Fact]
    public async Task FailedCallsDoNotExhaust()
        => await AssertBothModes(
            "import functools\n@functools.cache\ndef f(x):\n    raise ValueError(\"x\")\nfor i in range(1000):\n    try:\n        f(i)\n    except ValueError:\n        pass\nprint(\"done\")\n",
            "done\n");

    [Fact]
    public async Task CustomKeysHit()
        => await AssertBothModes(
            "import functools\nclass E:\n    def __eq__(self, other):\n        return True\n    def __hash__(self):\n        return 7\na = E()\n@functools.cache\ndef f(x):\n    return 1\nprint(f(a), f(E()), f.cache_info().hits)\n",
            "1 1 1\n");

    [Fact]
    public async Task HitsAndMisses()
        => await AssertBothModes(
            "import functools\n@functools.cache\ndef f(x):\n    return x * 2\nprint(f(21))\nprint(f(21))\nprint(f.cache_info())\n",
            "42\n42\nCacheInfo(hits=1, misses=1, maxsize=None, currsize=1)\n");

    [Fact]
    public async Task DisabledCacheAlwaysExecutes()
        => await AssertBothModes(
            "import functools\ncalls = []\n@functools.lru_cache(maxsize=0)\ndef f(x):\n    calls.append(x)\n    return x\nprint(f(1))\nprint(f(1))\nprint(len(calls))\nprint(f.cache_info())\n",
            "1\n1\n2\nCacheInfo(hits=0, misses=2, maxsize=0, currsize=0)\n");

    [Fact]
    public async Task UnboundedCacheRetains()
        => await AssertBothModes(
            "import functools\n@functools.cache\ndef f(x):\n    return x\nfor i in range(100):\n    f(i)\nprint(f.cache_info())\n",
            "CacheInfo(hits=0, misses=100, maxsize=None, currsize=100)\n");

    [Fact]
    public async Task RecursiveFunctionReenters()
        => await AssertBothModes(
            "import functools\n@functools.lru_cache(maxsize=None)\ndef fib(n):\n    return n if n < 2 else fib(n - 1) + fib(n - 2)\nprint(fib(20))\n",
            "6765\n");

    [Fact]
    public async Task CustomKeyReentrancyStaysCorrect()
        => await AssertBothModes(
            "import functools\ncalls = []\nguard = [False]\n@functools.lru_cache(maxsize=10)\ndef f(x):\n    return 1\nclass E:\n    def __hash__(self):\n        return 7\n    def __eq__(self, other):\n        if not guard[0]:\n            guard[0] = True\n            calls.append(f(100))\n        return True\na = E()\nprint(f(a))\nprint(f(E()))\nprint(calls)\n",
            "1\n1\n[1]\n");

    [Fact]
    public async Task InsertionDenialLeavesExactCharges()
    {
        const string code = "import functools\n@functools.lru_cache(maxsize=1)\ndef f(x):\n    return x\nreturn f(1)\n";
        var script = new LythonEngine().Compile(code);
        Assert.True(script.IsValid);
        var denied = script.Run(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 64 });
        Assert.False(denied.Success);
        var funded = script.Run(new MockLythonHost());
        Assert.True(funded.Success, funded.Failure?.Message);
        Assert.Equal(new BigInteger(1), funded.ReturnValue);
    }

    [Fact]
    public async Task KeywordAndTypedKeys()
        => await AssertBothModes(
            "import functools\n@functools.cache\ndef f(a, b=0):\n    return a + b\nprint(f(1, b=2))\nprint(f(1, b=2))\nprint(f.cache_info().hits)\n@functools.lru_cache(typed=True)\ndef g(x):\n    return 1\nprint(g(1))\nprint(g(True))\nprint(g.cache_info())\n@functools.lru_cache()\ndef h(x):\n    return 1\nprint(h(1))\nprint(h(True))\nprint(h.cache_info().hits)\n",
            "3\n3\n1\n1\n1\nCacheInfo(hits=0, misses=2, maxsize=128, currsize=2)\n1\n1\n1\n");

    [Fact]
    public async Task SameNamedTypesStayDistinctWhenTyped()
        => await AssertBothModes(
            "import functools\nclass E:\n    pass\nA = E\nclass E:\n    pass\nB = E\ncalls = []\n@functools.lru_cache(typed=True)\ndef f(x):\n    calls.append(1)\n    return 1\nf(A())\nf(B())\nprint(len(calls))\nprint(f.cache_info().misses)\n",
            "2\n2\n");

    [Fact]
    public async Task ClearAndRefill()
        => await AssertBothModes(
            "import functools\n@functools.lru_cache(maxsize=2)\ndef f(x):\n    return x\nprint(f(1))\nprint(f(2))\nf.cache_clear()\nprint(f.cache_info())\nprint(f(1))\nprint(f.cache_info())\n",
            "1\n2\nCacheInfo(hits=0, misses=0, maxsize=2, currsize=0)\n1\nCacheInfo(hits=0, misses=1, maxsize=2, currsize=1)\n");
}
