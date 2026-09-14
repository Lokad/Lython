using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// MG03/MG20: a __deepcopy__ hook may retain the implicit memo view past the
// copy. Each remembered entry owns durable view storage: stashed views keep
// working like CPython and keep every charge, while discarded views release
// through the reclamation pool instead of accumulating stale charges.
public sealed class CopyMemoViewAccountingScenarioTests
{
    private const long ThreeMib = 3145728;

    private static string StashSource(bool stash)
        => "import copy\n"
        + "saved = []\n"
        + "class C:\n"
        + "    def __deepcopy__(self, memo):\n"
        + "        if len(memo) == 1:\n"
        + (stash ? "            saved.append(memo)\n" : "            pass\n")
        + "        return None\n"
        + "items = [C() for i in range(200)]\n"
        + "for i in range(200):\n"
        + "    copy.deepcopy(items)\n"
        + "print(len(saved))\n"
        + (stash ? "print(sum(len(m) for m in saved))\n" : "")
        + "return 0\n";

    [Fact]
    public async Task StashedMemoViewsStayCharged()
    {
        var script = new LythonEngine().Compile(StashSource(true));
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = ThreeMib };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= ThreeMib);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= ThreeMib);
    }

    [Fact]
    public async Task UnstashedMemoViewsRelease()
    {
        var script = new LythonEngine().Compile(StashSource(false));
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
    public async Task FundedStashedViewsStayUsable()
    {
        var script = new LythonEngine().Compile(
            "import copy\n"
            + "saved = []\n"
            + "class C:\n"
            + "    def __deepcopy__(self, memo):\n"
            + "        if len(memo) == 1:\n"
            + "            saved.append(memo)\n"
            + "        return None\n"
            + "items = [C() for i in range(200)]\n"
            + "for i in range(200):\n"
            + "    copy.deepcopy(items)\n"
            + "m0 = saved.pop()\n"
            + "before = len(m0)\n"
            + "m0[\"sentinel\"] = None\n"
            + "return [len(saved), sum(len(m) for m in saved), len(m0) - before, \"sentinel\" in m0]\n");
        var expected = new List<object?> { new BigInteger(199), new BigInteger(39999), new BigInteger(1), true };
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16777216 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task HookFailureReleasesMemo()
    {
        var script = new LythonEngine().Compile(
            "import copy\n"
            + "class B:\n"
            + "    def __deepcopy__(self, memo):\n"
            + "        raise ValueError(\"nope\")\n"
            + "try:\n"
            + "    copy.deepcopy(B())\n"
            + "except ValueError:\n"
            + "    pass\n"
            + "return copy.deepcopy([1, 2])\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new BigInteger(1), new BigInteger(2) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }

    [Fact]
    public async Task RepeatedShallowCopiesReleaseMemo()
    {
        var script = new LythonEngine().Compile(
            "import copy\n"
            + "src = [1, 2]\n"
            + "i = 0\n"
            + "while i < 250:\n"
            + "    m = copy.copy(src)\n"
            + "    i = i + 1\n"
            + "return 0\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 65536 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
    }

    [Fact]
    public async Task ExplicitMemoDictsBehave()
    {
        var script = new LythonEngine().Compile(
            "import copy\n"
            + "d = {}\n"
            + "m = copy.deepcopy([1, [2]], d)\n"
            + "return [m, len(d) > 0]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new List<object?> { new BigInteger(1), new List<object?> { new BigInteger(2) } }, true };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
