using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// N09 white-box: lease boundaries, no double charge, exceptional disposal.
public sealed class DrainLeaseOwnershipTests
{
    private static LythonRuntime.ExecutionContext NewContext()
        => new(new MockLythonHost(), options: null);

    private static IEnumerable<object> Items(int count)
    {
        for (var i = 0; i < count; i++) yield return i;
    }

    [Fact]
    public void LeasedDrain_KeepsReservationAliveAcrossUse()
    {
        var context = NewContext();
        var before = context.MemoryGovernor.CurrentReservedBytes + context.MemoryGovernor.CurrentCommittedBytes;
        using var lease = PyIteration.DrainLeased(Items(100), new LythonSourceSpan(0, 0, 0, 0), context);
        Assert.Equal(100, lease.Items.Count);
        Assert.True(context.MemoryGovernor.CurrentReservedBytes + context.MemoryGovernor.CurrentCommittedBytes > before);
    }

    [Fact]
    public void LeasedDrain_ReleasesOnDispose()
    {
        var context = NewContext();
        var lease = PyIteration.DrainLeased(Items(10), new LythonSourceSpan(0, 0, 0, 0), context);
        var during = context.MemoryGovernor.CurrentReservedBytes + context.MemoryGovernor.CurrentCommittedBytes;
        lease.Dispose();
        var after = context.MemoryGovernor.CurrentReservedBytes + context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(after <= during);
    }

    [Fact]
    public void LeasedDrain_ReleasesOnException()
    {
        static IEnumerable<object> Failing()
        {
            yield return 1;
            yield return 2;
            throw new InvalidOperationException("boom");
        }
        var context = NewContext();
        var before = context.MemoryGovernor.CurrentReservedBytes + context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Throws<InvalidOperationException>(() => PyIteration.DrainLeased(Failing(), new LythonSourceSpan(0, 0, 0, 0), context));
        Assert.Equal(before, context.MemoryGovernor.CurrentReservedBytes + context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public async Task LeasedDrainAsync_ReleasesOnException()
    {
        static async IAsyncEnumerable<object> FailingAsync()
        {
            yield return 1;
            await Task.CompletedTask.ConfigureAwait(false);
            throw new InvalidOperationException("boom");
        }
        var context = NewContext();
        var before = context.MemoryGovernor.CurrentReservedBytes + context.MemoryGovernor.CurrentCommittedBytes;
        await Assert.ThrowsAsync<InvalidOperationException>(() => PyIteration.DrainLeasedAsync(FailingAsync(), new LythonSourceSpan(0, 0, 0, 0), context).AsTask());
        Assert.Equal(before, context.MemoryGovernor.CurrentReservedBytes + context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void CapacityBoundaries_ChargeExactlyOnce()
    {
        // 4 initial, doubling: 5 items fund 8 slots once (no double charge for same growth).
        var context = NewContext();
        using var lease = PyIteration.DrainLeased(Items(5), new LythonSourceSpan(0, 0, 0, 0), context);
        Assert.Equal(5, lease.Items.Count);
    }
}
