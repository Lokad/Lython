using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class InstanceLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private const string YieldingIterator = "class R:\n    def __init__(self):\n        self.n = 0\n    def __iter__(self):\n        return self\n    def __next__(self):\n        if self.n >= 3:\n            raise StopIteration()\n        self.n = self.n + 1\n        return self.n\n";

    private LythonRunOptions Budgeted(long budget) => new() { MaxExecutionMemoryBytes = budget };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task StatefulConstructDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(" + BatchIterations(100000) + "):\n    x = R()\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task PostInitStoreDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    pass\nfor i in range(" + BatchIterations(100000) + "):\n    x = R()\n    x.n = 1\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task MultiAttrStoreDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    pass\nfor i in range(" + BatchIterations(100000) + "):\n    x = R()\n    x.n = 1\n    x.m = 2\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task SetAttrDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    pass\nfor i in range(" + BatchIterations(100000) + "):\n    x = R()\n    setattr(x, \"n\", 1)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task YieldingUserIterableDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(" + BatchIterations(100000) + "):\n    for x in R():\n        pass\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task PartialAdvanceDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(" + BatchIterations(100000) + "):\n    it = iter(R())\n    y = next(it)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task BreakAbandonDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(" + BatchIterations(100000) + "):\n    for x in R():\n        break\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task CopyDiscardCompletes()
        => await AssertCompletes(
            "import copy\nclass R:\n    pass\nr = R()\nr.n = 1\nfor i in range(" + BatchIterations(100000) + "):\n    x = copy.copy(r)\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task DataclassConstructDiscardCompletes()
        => await AssertCompletes(
            "from dataclasses import dataclass\n@dataclass\nclass P:\n    x: int\nfor i in range(" + BatchIterations(50000) + "):\n    p = P(1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task AliasedInstancesDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(" + BatchIterations(100000) + "):\n    l = [R(), R()]\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task LiveGrowthBehaves()
        => await AssertCompletes(
            YieldingIterator + "r = R()\nfor i in range(" + BatchIterations(100000) + "):\n    r.n = i\nreturn r.n\n", (BatchIterations(100000) - 1).ToString(), Iterations(100000));

    [Fact]
    public async Task RetainedInstancesDenied()
    {
        var script = new LythonEngine().Compile(
            YieldingIterator + "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(R())\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(RetentionBudget);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RetentionBudget);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RetentionBudget);
    }

    [Fact]
    public async Task AttributeDeleteDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    pass\nfor i in range(" + BatchIterations(100000) + "):\n    x = R()\n    x.n = 1\n    del x.n\nreturn 0\n", "0", Iterations(100000));

    [Fact]
    public async Task RetainedAfterDeleteDenied()
    {
        var script = new LythonEngine().Compile(
            YieldingIterator + "objs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    o = R()\n    del o.n\n    objs.append(o)\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(RetentionBudget);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= RetentionBudget);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= RetentionBudget);
    }
}
