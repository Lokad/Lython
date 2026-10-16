using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Retained set storage stays charged. Finite host batches release their owners;
// clear/regrowth and aliases preserve Python semantics within each batch.
public sealed class SetShellAccountingScenarioTests
:LifetimeScenario{
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
    [Fact]
    public async Task DroppedEmptySetsRelease()
    {
        await AssertBatchCompletes("x = None\n"
            + "for i in range(8):\n"
            + "    x = set()\n"
            + "return 0\n", new System.Numerics.BigInteger(0), 3145728);
    }
    [Fact]
    public async Task DroppedSetDisplaysRelease()
    {
        await AssertBatchCompletes("x = set()\n"
            + "for i in range(8):\n"
            + "    x = {1, 2}\n"
            + "return len(x)\n", new System.Numerics.BigInteger(2), 3145728);
    }
    [Fact]
    public async Task DroppedSetComprehensionsRelease()
    {
        await AssertBatchCompletes("x = set()\n"
            + "for i in range(8):\n"
            + "    x = {j for j in range(3)}\n"
            + "return len(x)\n", new System.Numerics.BigInteger(3), 3145728);
    }
    [Fact]
    public async Task GrownDroppedSetsFit()
    {
        await AssertBatchCompletes("x = None\n"
            + "for i in range(8):\n"
            + "    x = set()\n"
            + "    x.add(i)\n"
            + "return 0\n", new System.Numerics.BigInteger(0), 3145728);
    }
    [Fact]
    public async Task ClearedDisplaysReleaseExactly()
    {
        await AssertBatchCompletes("x = set()\n"
            + "for i in range(8):\n"
            + "    x = {1, 2, 3}\n"
            + "    x.clear()\n"
            + "return len(x)\n", new System.Numerics.BigInteger(0), 3145728);
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
