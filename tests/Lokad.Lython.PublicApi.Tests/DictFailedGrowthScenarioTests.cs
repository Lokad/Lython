using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG05: dict growth stays charged as it scales, so later insertions cannot
/// ride enlarged uncharged capacity. Denial choreography lives in the white-box
/// exact-denial test: a coupon-scale denial leaves too little slack to construct
/// its own catchable exception, so tiny-budget catch-and-continue scripts cannot
/// observe recovery.
/// </summary>
public sealed class DictFailedGrowthScenarioTests
{
    [Fact]
    public async Task DictGrowthChargesAdoptedCoupons()
    {
        // MG05: dict growth stays charged as it scales, so later insertions
        // cannot ride enlarged uncharged capacity. N06: 3000 distinct int keys
        // adopt one 64 B coupon each beside table and shell (peak 300556 in both
        // modes); the floor below fails if coupons ever stop committing, while
        // catch-and-continue denial choreography lives in the white-box
        // exact-denial test (a coupon-scale denial leaves too little slack to
        // even construct its own catchable exception).
        var script = new LythonEngine().Compile(
            """
            v = 1
            d = {i: v for i in range(3000)}
            return len(d)
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
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
