using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG05: dict.update pair validation must not materialize the pair first: a
/// million-item pair raises the same ValueError either way, but pre-fix it
/// allocated tens of megabytes of transient list backing to discover that.
/// The drill measures per-thread allocations around a synchronous failure.
/// (Pairs arrive through a parameter; unbounded iterables validate with a
/// bounded pull horizon, so the message stays generic past it.)
/// </summary>
public sealed class DictUpdatePairsAccountingTests
{
    private const string OversizedPair =
        "def upd(pairs):\n    d = {}\n    d.update(pairs)\n    return 0\nupd([(x for x in range(1000000))])\n";

    [Fact]
    public void OversizedPairAllocatesNoProportionalTransient()
    {
        var script = new LythonEngine().Compile(OversizedPair);
        Assert.True(script.IsValid);
        // Warm up JIT and caches so the measured run counts steady-state work.
        _ = script.Run(new MockLythonHost());

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = script.Run(new MockLythonHost());
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.False(result.Success);
        Assert.Equal("ValueError", result.Failure?.ExceptionType);
        Assert.Equal("dictionary update sequence element has length other than 2", result.Failure?.Message);
        Assert.True(allocated < 8388608, $"pair validation allocated {allocated} bytes before failing");
    }
}
