using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: datetime construction shells own lifetime like the other factory results do.
public class DatetimeLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);
    private long RetentionBudget => Budget(1048576);

    private LythonRunOptions Budgeted() => new() { MaxExecutionMemoryBytes = StreamingBudget };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task DateCtorDiscardCompletes()
        => await AssertCompletes(
            "import datetime\nfor i in range(" + BatchIterations(50000) + "):\n    x = datetime.date(2020, 1, 1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task TimedeltaCtorDiscardCompletes()
        => await AssertCompletes(
            "import datetime\nfor i in range(" + BatchIterations(50000) + "):\n    x = datetime.timedelta(days=1)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DateArithDiscardCompletes()
        => await AssertCompletes(
            "import datetime\nd = datetime.date(2020, 1, 1)\nt = datetime.timedelta(days=1)\nfor i in range(" + BatchIterations(50000) + "):\n    x = d + t\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task DatetimeBehaves()
        => await AssertCompletes(
            "import datetime\nd = datetime.date(2020, 1, 1) + datetime.timedelta(days=1)\nreturn str(d) + str(d.year)\n", "2020-01-022020");

    [Fact]
    public async Task RetainedDateDenied()
    {
        var script = new LythonEngine().Compile(
            "import datetime\nobjs = []\ni = 0\nwhile i < " + Iterations(20000) + ":\n    objs.append(datetime.date(2020, 1, 1))\n    i = i + 1\nreturn len(objs)\n");
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
