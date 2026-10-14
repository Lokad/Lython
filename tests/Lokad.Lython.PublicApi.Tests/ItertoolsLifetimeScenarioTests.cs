using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class ItertoolsLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);

    private LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = StreamingBudget };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task CountDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.count()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task RepeatDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.repeat(1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task CycleDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.cycle([1])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ChainDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.chain([1], [2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task IsliceDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.islice([1, 2], 1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task BatchedDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.batched([1, 2], 1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task GroupbyDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.groupby([1])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task TeeDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.tee([1])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ZipLongestDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.zip_longest([1], [2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PairwiseDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.pairwise([1, 2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task AccumulateDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.accumulate([1, 2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task CompressDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.compress([1], [1])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task FilterFalseDiscardCompletes()
        => await AssertCompletes(
            "import itertools\ndef f(x):\n    return False\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.filterfalse(f, [1])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DropWhileDiscardCompletes()
        => await AssertCompletes(
            "import itertools\ndef f(x):\n    return False\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.dropwhile(f, [1])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task TakeWhileDiscardCompletes()
        => await AssertCompletes(
            "import itertools\ndef f(x):\n    return True\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.takewhile(f, [1])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task StarmapDiscardCompletes()
        => await AssertCompletes(
            "import itertools\ndef f(a, b):\n    return a\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.starmap(f, [(1, 2)])\nreturn 0\n", "0", Iterations(50000));

    // Pool-owning factories: product/combinations/permutations shells ride with their
    // materialized pools and index tables under one coupon mirroring all committed
    // charges. Consumed per-item tuples track at their production site like zip items.
    [Fact]
    public async Task ProductDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.product([1], [2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task CombinationsDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.combinations([1, 2], 1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PermutationsDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = itertools.permutations([1, 2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task BatchedConsumedDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = list(itertools.batched([1], 1))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PairwiseConsumedDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = list(itertools.pairwise([1, 2]))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task GroupbyConsumedDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = list(itertools.groupby([1]))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task CombinationsConsumedDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = list(itertools.combinations([1, 2], 2))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ZipLongestConsumedDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = list(itertools.zip_longest([1], [2]))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ProductConsumedDiscardCompletes()
        => await AssertCompletes(
            "import itertools\nfor i in range(" + BatchIterations(50000) + "):\n    x = list(itertools.product([1], [2]))\nreturn 0\n", "0", Iterations(50000));

}
