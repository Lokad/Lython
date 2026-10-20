using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// Dictionary entry and backing growth stay funded as the dictionary scales.
/// Refused admission is terminal; white-box tests verify transactional balances.
/// </summary>
public sealed class DictFailedGrowthScenarioTests
{
    [Fact]
    public async Task DictGrowthChargesRetainedEntriesAndCapacity()
    {
        // A substantial dictionary must report substantial retained funding;
        // this floor catches missing entry charges without pinning tiny-budget
        // thresholds or promising recovery after refused admission.
        var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(
            """
            v = 1
            d = {i: v for i in range(3000)}
            return len(d)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1048576) };
        var expected = new BigInteger(3000);
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(expected, sync.ReturnValue);
        Assert.True(sync.PeakExecutionMemoryBytes >= 200000, $"peak {sync.PeakExecutionMemoryBytes}");
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(expected, asyncResult.ReturnValue);
        Assert.True(asyncResult.PeakExecutionMemoryBytes >= 200000, $"peak {asyncResult.PeakExecutionMemoryBytes}");
    }
}
