using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class GrowthStorageScenarioTests : LifetimeScenario
{
    // One 1,000-item growth cycle exercises capacity changes at the fixed cap.
    protected override int BatchSize => 1;
    protected virtual long AppendBudget => 98304;
    protected virtual int AppendIterations => 4096;

    private long StreamingBudget => Budget(3145728);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    [Fact]
    public async Task ListGrowDiscardCompletes()
    {
        await AssertBatchCompletes(
            "for i in range(" + BatchIterations(2000) + "):\n    x = []\n    for j in range(1000):\n        x.append(None)\nreturn 0\n", "0", StreamingBudget, Iterations(2000));
    }

    [Fact]
    public async Task ListRepeatCompletes()
    {
        await AssertBatchCompletes(
            "for i in range(" + BatchIterations(2000) + "):\n    x = [None] * 1000\nreturn 0\n", "0", StreamingBudget, Iterations(2000));
    }

    [Fact]
    public async Task DictGrowDiscardCompletes()
    {
        await AssertBatchCompletes(
            "for i in range(" + BatchIterations(1000) + "):\n    d = {}\n    for j in range(100):\n        d[j] = j\nreturn 0\n", "0", StreamingBudget, Iterations(1000));
    }

    [Fact]
    public async Task SetGrowDiscardCompletes()
    {
        await AssertBatchCompletes(
            "for i in range(" + BatchIterations(10000) + "):\n    s = set()\n    for j in range(100):\n        s.add(j)\nreturn 0\n", "0", StreamingBudget, Iterations(10000));
    }

    [Fact]
    public async Task NonAsciiSliceDiscardCompletes()
    {
        await AssertBatchCompletes(
            "for i in range(" + BatchIterations(2000) + "):\n    x = '\u00e9' * 1000\n    y = x[1:]\nreturn 0\n", "0", StreamingBudget, Iterations(2000));
    }

    [Fact]
    public async Task AsciiSliceDiscardCompletes()
    {
        await AssertBatchCompletes(
            "for i in range(" + BatchIterations(2000) + "):\n    x = 'a' * 1000\n    y = x[1:]\nreturn 0\n", "0", StreamingBudget, Iterations(2000));
    }

    [Fact]
    public async Task RetainedListGrowthDenies()
    {
        // Honest retention still trips: a fully retained 100k list cannot fit 1 MiB.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "x = []\nfor j in range(" + Iterations(100000) + "):\n    x.append(j)\nreturn len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Budget(1048576)) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
    }

    [Fact]
    public async Task AliasedListGrowthShared()
    {
        // Growth through one alias is visible through the other with a single
        // backing charge: coupons re-snapshot, they never double-commit.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "x = []\nfor j in range(100):\n    x.append(j)\ny = x\nfor j in range(100, 200):\n    y.append(j)\nreturn len(x)\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new BigInteger(200);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(Budget(1048576)) };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }


}
