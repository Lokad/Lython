using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ImmutableStorageLifetimeTests : LifetimeScenario
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SharedLargePayloadKeepsIdentityUnderConservativeSlotCharges(bool named)
    {
        var declaration = named ? "from collections import namedtuple\nRow = namedtuple('Row', 'a b c')\n" : "";
        var value = named ? "Row(large, large, large)" : "(large, large, large)";
        return AssertBatchCompletes(declaration +
            $"large = 1 << 8192\nrecords = [{value} for _ in range(32)]\nreturn [len(records), records[-1][0] is records[-1][1]]",
            new List<object?> { new BigInteger(32), true }, 1024 * 1024);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RowBatchesPreserveTheCompleteSumAndRetainedRowsDeny(bool named)
    {
        var declaration = named ? "from collections import namedtuple\nRow = namedtuple('Row', 'a b c')\n" : "";
        var value = named ? "Row(i, i, i + 1)" : "(i, i, i + 1)";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var streaming = new LythonEngine().Compile(declaration +
            $"total = 0\nfor i in range(start, start + count):\n row = {value}\n assert row[0] is row[1]\n total += row[0]\nreturn total\n", budget);
        using var retaining = new LythonEngine().Compile(declaration +
            $"rows = []\nfor i in range(20000):\n rows.append({value})\nreturn len(rows)\n", budget);
        Assert.True(streaming.IsValid);
        Assert.True(retaining.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asyncRun in new[] { false, true })
        {
            long sum = 0;
            for (var offset = 0; offset < 5000; offset += 32)
            {
                var count = Math.Min(32, 5000 - offset);
                var options = new LythonRunOptions
                {
                    MemoryBudget = new LythonMemoryBudget(1024 * 1024),
                    Globals = new Dictionary<string, object?> { ["start"] = offset, ["count"] = count }
                };
                using (var result = asyncRun ? await streaming.RunAsync(new MockLythonHost(), options) : streaming.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    var expected = new BigInteger((2L * offset + count - 1) * count / 2);
                    Assert.Equal(expected, result.ReturnValue);
                    sum += (long)expected;
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(12497500L, sum);
            using (var retained = asyncRun ? await retaining.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) })
                : retaining.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) }))
            {
                Assert.False(retained.Success);
                Assert.Equal("MemoryError", retained.Failure?.ExceptionType);
                Assert.InRange(retained.PeakExecutionMemoryBytes, 1, 1024 * 1024);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        streaming.Dispose();
        retaining.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task AdequatelyFundedAliasesKeepTheirIdentity()
    {
        var script = new LythonEngine().Compile("x = 12345\nt = (x,) * 100000\nreturn [len(t), t[0] is t[-1]]");
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16 * 1024 * 1024) };
        var expected = new List<object?> { new BigInteger(100000), true };
        var sync = script.Run(new MockLythonHost(), options);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
            Assert.True(result.PeakExecutionMemoryBytes <= Assert.IsType<LythonMemoryBudget>(options.MemoryBudget).LimitBytes);
        }
    }
}
