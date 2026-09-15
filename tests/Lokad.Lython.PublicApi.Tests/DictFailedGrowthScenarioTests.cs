using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

/// <summary>
/// MG05: a failed dict resize must not leave enlarged uncharged capacity
/// behind for later insertions to ride for free. The stale committed-capacity
/// field forces every post-failure insertion through the budget check again,
/// mirroring FailedSetGrowthLeavesNothingUsable. The tail returns a scalar so the assertions need no execution headroom past the denial point.
/// </summary>
public sealed class DictFailedGrowthScenarioTests
{
    [Fact]
    public async Task FailedDictGrowthLeavesNothingUsable()
    {
        var script = new LythonEngine().Compile(
            """
            def fill(d, start, count):
                added = 0
                i = 0
                while i < count:
                    try:
                        d[start + i] = 1
                    except MemoryError:
                        return added
                    added = added + 1
                    i = i + 1
                return added
            d = {}
            first = fill(d, 0, 20000)
            second = fill(d, 100000, 500)
            if first < 20000:
                return second
            return -1
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 131072 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(0), Assert.IsType<BigInteger>(sync.ReturnValue));

        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(0), Assert.IsType<BigInteger>(asyncResult.ReturnValue));
    }
}
