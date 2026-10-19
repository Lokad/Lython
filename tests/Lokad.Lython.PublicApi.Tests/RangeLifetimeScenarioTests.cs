using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class RangeLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);


    [Fact]
    public async Task RangesDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = range(2)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task EnumerateDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    x = enumerate([1])\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task MapDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    a = list(map(str, [1]))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task FilterDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    b = list(filter(None, [1]))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ZipDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    c = list(zip([1], [2]))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task GeneratorsDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(100000) + "):\n    g = (x for x in [1])\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task EnumerateConsumedDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    c = list(enumerate([1]))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task TupleZipDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    c = tuple(zip([1], [2]))\nreturn 0\n", "0", Iterations(50000));

    // Shared-budget refusal and cursor alignment are covered by
    // GovernedCursorAdmissionTests, without historical registry-fee thresholds.

    // Shell exhaustion: 256 covers setup and the one-item source display but not the
    // pooled shell, so construction must deny cleanly instead of stranding or crashing.
    private static async Task AssertDenies(string source, long budget)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(budget) });
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        var asyncResult = await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(budget) });
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task EnumerateShellDeniesCleanly()
        => await AssertDenies("x = enumerate([1])\nreturn 0\n", 256);

    // Reversed/iter shells: builtin branches, iter() shapes and member dunders all own
    // one pooled shell charge per live instance. Each failed pre-fix with a stranded
    // 128B shell per iteration; set/member __iter__ sites stay a residual (explicit
    // dunder calls only, never loop iteration).
    [Fact]
    public async Task ReversedListDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = reversed([1, 2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ReversedRangeDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = reversed(range(3))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ReversedStrDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = reversed(\"ab\")\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ReversedDictDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = reversed({1: 2})\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task IterListDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = iter([1, 2])\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task IterRangeDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = iter(range(3))\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task IterStrDiscardCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = iter(\"ab\")\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task ListDunderReversedCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = [1, 2].__reversed__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DequeDunderReversedCompletes()
        => await AssertCompletes(
            "import collections\nd = collections.deque([1, 2])\nfor i in range(" + BatchIterations(50000) + "):\n    x = d.__reversed__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DictDunderReversedCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(" + BatchIterations(50000) + "):\n    x = d.__reversed__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DictKeysDunderReversedCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(" + BatchIterations(50000) + "):\n    x = d.keys().__reversed__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DictValuesDunderReversedCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(" + BatchIterations(50000) + "):\n    x = d.values().__reversed__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DictItemsDunderReversedCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(" + BatchIterations(50000) + "):\n    x = d.items().__reversed__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DefaultDictDunderReversedCompletes()
        => await AssertCompletes(
            "import collections\nd = collections.defaultdict(list)\nd[\"k\"] = 1\nfor i in range(" + BatchIterations(50000) + "):\n    x = d.__reversed__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task CounterDunderReversedCompletes()
        => await AssertCompletes(
            "import collections\nc = collections.Counter(\"ab\")\nfor i in range(" + BatchIterations(50000) + "):\n    x = c.__reversed__()\nreturn 0\n", "0", Iterations(50000));

    // Member __iter__ dunders construct the same pooled shells as iter(); explicit
    // dunder calls in a loop strand without the call-site ownership below.
    [Fact]
    public async Task ListIterDunderCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = [1, 2].__iter__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DictKeysIterDunderCompletes()
        => await AssertCompletes(
            "d = {1: 2}\nfor i in range(" + BatchIterations(50000) + "):\n    x = d.keys().__iter__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task StrIterDunderCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = \"ab\".__iter__()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task SetIterDunderCompletes()
        => await AssertCompletes(
            "for i in range(" + BatchIterations(50000) + "):\n    x = {1, 2}.__iter__()\nreturn 0\n", "0", Iterations(50000));

}
