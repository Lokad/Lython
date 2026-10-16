using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public class SliceLifetimeScenarioTests : LifetimeScenario
{
    protected virtual int SliceLength => 64;

    private long StreamingBudget => Budget(3145728);

    [Fact]
    public async Task BoundedStringSlicesComplete()
    {
        await AssertBatchCompletes(
            "b = 'a' * " + SliceLength + "\n"
            + "x = ''\n"
            + "for i in range(" + BatchIterations(10000) + "):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n", (SliceLength - 1).ToString(), StreamingBudget, Iterations(10000));
    }

    [Fact]
    public async Task BoundedListSlicesComplete()
    {
        await AssertBatchCompletes(
            "b = list(range(" + SliceLength + "))\n"
            + "x = []\n"
            + "for i in range(" + BatchIterations(10000) + "):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n", (SliceLength - 1).ToString(), StreamingBudget, Iterations(10000));
    }

    [Fact]
    public async Task BoundedTupleSlicesComplete()
    {
        await AssertBatchCompletes(
            "b = tuple(range(" + SliceLength + "))\n"
            + "x = ()\n"
            + "for i in range(" + BatchIterations(10000) + "):\n"
            + "    x = b[1:]\n"
            + "return len(x)\n", (SliceLength - 1).ToString(), StreamingBudget, Iterations(10000));
    }

    [Fact]
    public async Task RetainedSlicesStayUsableAndCleanupAllowsAnotherBatch()
    {
        var budget = new LythonMemoryBudget(Math.Max(512 * 1024, StreamingBudget + 256 * 1024));
        using var script = new LythonEngine().Compile($$"""
            b = 'a' * {{SliceLength}}
            objs = []
            for i in range(count):
                objs.append(b[1:])
            return objs[-1]
            """, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = StreamingBudget, Globals = new Dictionary<string, object?> { ["count"] = 2 } };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new string('a', SliceLength - 1), result.ReturnValue);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            var refusalOptions = new LythonRunOptions { MaxExecutionMemoryBytes = StreamingBudget,
                Globals = new Dictionary<string, object?> { ["count"] = Iterations(100000) } };
            using (var denied = asynchronous ? await script.RunAsync(new MockLythonHost(), refusalOptions) : script.Run(new MockLythonHost(), refusalOptions))
            {
                Assert.False(denied.Success);
                Assert.Equal("MemoryError", denied.Failure?.ExceptionType);
                Assert.Equal(new string('a', SliceLength - 1), result.ReturnValue);
            }
            Assert.Equal(retained, budget.ReservedBytes);
            using var resumed = new LythonEngine().Run($"return len('a' * {SliceLength - 1})", new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = budget, MaxExecutionMemoryBytes = StreamingBudget });
            Assert.True(resumed.Success, resumed.Failure?.Message);
            Assert.Equal(new BigInteger(SliceLength - 1), resumed.ReturnValue);
            resumed.Dispose();
            Assert.Equal(retained, budget.ReservedBytes);
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task SliceAliasesStayUsable()
    {
        var script = new LythonEngine().Compile(
            "b = 'a' * " + SliceLength + "\n"
            + "s = b[1:]\n"
            + "t = s\n"
            + "return [len(s), len(t)]\n");
        Assert.True(script.IsValid, string.Join("|", script.Diagnostics.Select(d => d.Code + ":" + d.Message)));
        var expected = new List<object?> { new BigInteger(SliceLength - 1), new BigInteger(SliceLength - 1) };
        var sync = script.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);

        var asyncResult = await script.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
    }
}
