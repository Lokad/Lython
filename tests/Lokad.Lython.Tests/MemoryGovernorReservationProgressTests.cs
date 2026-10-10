using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class MemoryGovernorReservationProgressTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulReservationAllowsTheNextAllocationToCollectRemainingGarbage(bool temporary)
    {
        var governor = new MemoryGovernor(600);
        var pool = new ChargeReclamationPool(governor);
        governor.LivePoolProvider = () => [pool];
        var targets = CreateCollectedAndUncollectedValues(governor, pool, retainSecond: false);
        Assert.False(IsAlive(targets.Collected));
        Assert.True(IsAlive(targets.Uncollected));
        Assert.Equal(544, governor.CurrentCommittedBytes);

        // A partial drain refunds the already-collected value, enough to fund
        // scratch without collecting the remaining garbage. No commit happens
        // between this scratch reservation and the next payload allocation.
        using var scratch = temporary ? governor.ReserveTemporary(200, null) : null;
        if (!temporary) governor.Reserve(200, null);
        Assert.Equal(320, governor.CurrentCommittedBytes);
        Assert.Equal(200, governor.CurrentReservedBytes);
        Assert.True(IsAlive(targets.Uncollected));

        governor.Reserve(150, null);
        Assert.False(IsAlive(targets.Uncollected));
        Assert.Equal(0, pool.Count);
        Assert.Equal(64, governor.CurrentCommittedBytes);
        Assert.Equal(350, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.LastDeniedReservationBytes);
        Assert.True(governor.PeakAccountedBytes <= 600);
        governor.ReleaseReserved(150);
        if (!temporary) governor.ReleaseReserved(200);
    }

    [Fact]
    public void PinnedDenialAfterSuccessfulScratchDoesNotRepeatedlyCollectWithoutProgress()
    {
        var governor = new MemoryGovernor(600);
        var pool = new ChargeReclamationPool(governor);
        var enumerations = 0;
        governor.LivePoolProvider = () => { enumerations++; return [pool]; };
        var targets = CreateCollectedAndUncollectedValues(governor, pool, retainSecond: true);
        using var scratch = governor.ReserveTemporary(200, null);
        Assert.Equal(320, governor.CurrentCommittedBytes);
        Assert.Equal(200, governor.CurrentReservedBytes);
        var beforeDenial = enumerations;

        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(150, null));
        Assert.True(enumerations > beforeDenial);
        var afterDenial = enumerations;
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(150, null));
        Assert.Equal(afterDenial, enumerations);

        // Merely checking capacity or making a zero-byte reservation cannot
        // manufacture progress and make caught, pinned failures collect again.
        governor.EnsureCanReserve(1, null);
        governor.Reserve(0, null);
        governor.Commit(0);
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(150, null));
        Assert.Equal(afterDenial, enumerations);
        Assert.Equal(320, governor.CurrentCommittedBytes);
        Assert.Equal(200, governor.CurrentReservedBytes);
        Assert.Equal(150, governor.LastDeniedReservationBytes);
        Assert.Equal(1, pool.Count);
        GC.KeepAlive(targets.Retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<object> Collected, WeakReference<object> Uncollected, object? Retained)
        CreateCollectedAndUncollectedValues(MemoryGovernor governor, ChargeReclamationPool pool, bool retainSecond)
    {
        var collected = AddDroppedValue(governor, pool);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var value = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        pool.Track(value, 128);
        return (collected, new WeakReference<object>(value), retainSecond ? value : null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> AddDroppedValue(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var value = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        pool.Track(value, 128);
        return new WeakReference<object>(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<object> target) => target.TryGetTarget(out _);
}
