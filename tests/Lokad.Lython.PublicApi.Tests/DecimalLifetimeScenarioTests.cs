using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: decimal construction shells own lifetime like the other factory results do.
public class DecimalLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task DecimalCtorDiscardCompletes()
        => await AssertCompletes(
            "import decimal\nfor i in range(" + BatchIterations(50000) + "):\n    x = decimal.Decimal('1.5')\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DecimalArithDiscardCompletes()
        => await AssertCompletes(
            "import decimal\na = decimal.Decimal('1.5')\nb = decimal.Decimal('2.5')\nfor i in range(" + BatchIterations(50000) + "):\n    x = a + b\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DecimalBehaves()
        => await AssertCompletes(
            "import decimal\nreturn str(decimal.Decimal('1.5') + decimal.Decimal('2.5'))\n", "4.0");

    [Fact]
    public async Task RetainedDecimalDenied()
    {
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            "import decimal\nobjs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(decimal.Decimal('1.5'))\n    i = i + 1\nreturn len(objs)\n");
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(RetentionBudget) };
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
