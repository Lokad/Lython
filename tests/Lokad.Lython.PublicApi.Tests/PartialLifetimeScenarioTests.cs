using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: partial construction results own lifetime like the other factory results do.
public class PartialLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = StreamingBudget };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task PartialDiscardCompletes()
        => await AssertCompletes(
            "import functools\nfor i in range(" + BatchIterations(50000) + "):\n    x = functools.partial(int, base=2)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PartialMethodDiscardCompletes()
        => await AssertCompletes(
            "import functools\nfor i in range(" + BatchIterations(50000) + "):\n    x = functools.partialmethod(int, base=2)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task WrapsDiscardCompletes()
        => await AssertCompletes(
            "import functools\nfor i in range(" + BatchIterations(50000) + "):\n    x = functools.wraps(int)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PartialMetadataGrowthCompletes()
        => await AssertCompletes(
            "import functools\np = functools.partial(int, base=2)\nfor i in range(" + BatchIterations(50000) + "):\n    p.new_attr = 1\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task PartialBehaves()
        => await AssertCompletes(
            "import functools\np = functools.partial(int, base=2)\nreturn str([p(\"11\"), len(p.args), len(p.keywords)])\n", "[3, 0, 1]");

    [Fact]
    public async Task RetainedPartialDenied()
    {
        var script = new LythonEngine().Compile(
            "import functools\nobjs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(functools.partial(int, base=2))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = RetentionBudget };
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
