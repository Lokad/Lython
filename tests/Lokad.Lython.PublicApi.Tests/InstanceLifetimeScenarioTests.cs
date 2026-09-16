using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// M04: instance attribute slots commit 64 B per new key with no owning coupon,
// so dropped stateful instances stranded their attribute storage. Stores adopt
// (or re-snapshot) through the pool at the __setattr__ slot, so discards
// reclaim on sweep while retained instances stay charged. This also closes the
// yielding user-iterator workload: every failing shape constructed a stateful
// iterator per iteration.
public sealed class InstanceLifetimeScenarioTests
{
    private const long ThreeMib = 3145728;
    private const long OneMib = 1048576;

    private const string YieldingIterator = "class R:\n    def __init__(self):\n        self.n = 0\n    def __iter__(self):\n        return self\n    def __next__(self):\n        if self.n >= 3:\n            raise StopIteration()\n        self.n = self.n + 1\n        return self.n\n";

    private static LythonRunOptions Budgeted(long budget) => new() { MaxExecutionMemoryBytes = budget };

    private static async Task AssertCompletes(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var sync = script.Run(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue?.ToString());
        var asyncResult = await script.RunAsync(new MockLythonHost(), Budgeted(ThreeMib));
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue?.ToString());
    }

    [Fact]
    public async Task StatefulConstructDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(100000):\n    x = R()\nreturn 0\n", "0");

    [Fact]
    public async Task PostInitStoreDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    pass\nfor i in range(100000):\n    x = R()\n    x.n = 1\nreturn 0\n", "0");

    [Fact]
    public async Task MultiAttrStoreDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    pass\nfor i in range(100000):\n    x = R()\n    x.n = 1\n    x.m = 2\nreturn 0\n", "0");

    [Fact]
    public async Task SetAttrDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    pass\nfor i in range(100000):\n    x = R()\n    setattr(x, \"n\", 1)\nreturn 0\n", "0");

    [Fact]
    public async Task YieldingUserIterableDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(100000):\n    for x in R():\n        pass\nreturn 0\n", "0");

    [Fact]
    public async Task PartialAdvanceDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(100000):\n    it = iter(R())\n    y = next(it)\nreturn 0\n", "0");

    [Fact]
    public async Task BreakAbandonDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(100000):\n    for x in R():\n        break\nreturn 0\n", "0");

    [Fact]
    public async Task CopyDiscardCompletes()
        => await AssertCompletes(
            "import copy\nclass R:\n    pass\nr = R()\nr.n = 1\nfor i in range(100000):\n    x = copy.copy(r)\nreturn 0\n", "0");

    [Fact]
    public async Task DataclassConstructDiscardCompletes()
        => await AssertCompletes(
            "from dataclasses import dataclass\n@dataclass\nclass P:\n    x: int\nfor i in range(50000):\n    p = P(1)\nreturn 0\n", "0");

    [Fact]
    public async Task AliasedInstancesDiscardCompletes()
        => await AssertCompletes(
            YieldingIterator + "for i in range(100000):\n    l = [R(), R()]\nreturn 0\n", "0");

    [Fact]
    public async Task LiveGrowthBehaves()
        => await AssertCompletes(
            YieldingIterator + "r = R()\nfor i in range(100000):\n    r.n = i\nreturn r.n\n", "99999");

    [Fact]
    public async Task RetainedInstancesDenied()
    {
        var script = new LythonEngine().Compile(
            YieldingIterator + "objs = []\ni = 0\nwhile i < 20000:\n    objs.append(R())\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(OneMib);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }

    [Fact]
    public async Task AttributeDeleteDiscardCompletes()
        => await AssertCompletes(
            "class R:\n    pass\nfor i in range(100000):\n    x = R()\n    x.n = 1\n    del x.n\nreturn 0\n", "0");

    [Fact]
    public async Task RetainedAfterDeleteDenied()
    {
        var script = new LythonEngine().Compile(
            YieldingIterator + "objs = []\ni = 0\nwhile i < 20000:\n    o = R()\n    del o.n\n    objs.append(o)\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = Budgeted(OneMib);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.False(sync.Success);
        Assert.Equal("MemoryError", sync.Failure?.ExceptionType);
        Assert.True(sync.PeakExecutionMemoryBytes <= OneMib);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.False(asyncResult.Success);
        Assert.Equal("MemoryError", asyncResult.Failure?.ExceptionType);
        Assert.True(asyncResult.PeakExecutionMemoryBytes <= OneMib);
    }
}
