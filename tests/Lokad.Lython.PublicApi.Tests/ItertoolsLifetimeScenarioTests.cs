using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M05: itertools factory shells own one charge per live instance with reclamation
// through the pool once dropped. Product/combinations/permutations shells ride with
// their materialized pools (separate commit); consumed per-item tuples likewise.
public sealed class ItertoolsLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;

    private static LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = ThreeMib };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task CountDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.count()\nreturn 0\n", "0");

    [Fact]
    public async Task RepeatDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.repeat(1)\nreturn 0\n", "0");

    [Fact]
    public async Task CycleDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.cycle([1])\nreturn 0\n", "0");

    [Fact]
    public async Task ChainDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.chain([1], [2])\nreturn 0\n", "0");

    [Fact]
    public async Task IsliceDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.islice([1, 2], 1)\nreturn 0\n", "0");

    [Fact]
    public async Task BatchedDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.batched([1, 2], 1)\nreturn 0\n", "0");

    [Fact]
    public async Task GroupbyDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.groupby([1])\nreturn 0\n", "0");

    [Fact]
    public async Task TeeDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.tee([1])\nreturn 0\n", "0");

    [Fact]
    public async Task ZipLongestDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.zip_longest([1], [2])\nreturn 0\n", "0");

    [Fact]
    public async Task PairwiseDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.pairwise([1, 2])\nreturn 0\n", "0");

    [Fact]
    public async Task AccumulateDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.accumulate([1, 2])\nreturn 0\n", "0");

    [Fact]
    public async Task CompressDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(50000):\n    x = itertools.compress([1], [1])\nreturn 0\n", "0");

    [Fact]
    public async Task FilterFalseDiscardCompletes()
        => await AssertCompletes(
            "import itertools\ndef f(x):\n    return False\nfor i in range(50000):\n    x = itertools.filterfalse(f, [1])\nreturn 0\n", "0");

    [Fact]
    public async Task DropWhileDiscardCompletes()
        => await AssertCompletes(
            "import itertools\ndef f(x):\n    return False\nfor i in range(50000):\n    x = itertools.dropwhile(f, [1])\nreturn 0\n", "0");

    [Fact]
    public async Task TakeWhileDiscardCompletes()
        => await AssertCompletes(
            "import itertools\ndef f(x):\n    return True\nfor i in range(50000):\n    x = itertools.takewhile(f, [1])\nreturn 0\n", "0");

    [Fact]
    public async Task StarmapDiscardCompletes()
        => await AssertCompletes(
            "import itertools\ndef f(a, b):\n    return a\nfor i in range(50000):\n    x = itertools.starmap(f, [(1, 2)])\nreturn 0\n", "0");
}
