using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class MemoryGovernorGenerationReliefTests
{
    private static readonly LythonSourceSpan Span = new(14, 2, 3, 1);

    [Fact]
    public void OldDroppedStorageReclaimsBeforeDenial()
    {
        var governor = new MemoryGovernor(16384);
        var pool = new ChargeReclamationPool(governor);
        governor.ReclamationPool = pool;
        var target = CreateOldStorage(pool, governor, retainAnother: false).Dropped;
        var pinned = PinRemaining(governor);
        Assert.True(IsAlive(target));
        governor.Reserve(64, Span);
        Assert.False(IsAlive(target));
        Assert.Equal(0, pool.Count);
        Assert.Equal(pinned + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.Equal(64, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.LastDeniedReservationBytes);
        governor.ReleaseReserved(64);
    }

    [Fact]
    public void RetainedOldAliasKeepsFundingWhileOtherOldStorageReclaims()
    {
        var governor = new MemoryGovernor(16384);
        var pool = new ChargeReclamationPool(governor);
        governor.ReclamationPool = pool;
        var storage = CreateOldStorage(pool, governor, retainAnother: true);
        var retained = Assert.IsType<PyList>(storage.Retained);
        var pinned = PinRemaining(governor);
        Assert.True(IsAlive(storage.Dropped));
        governor.Reserve(64, Span);
        Assert.False(IsAlive(storage.Dropped));
        Assert.Equal(1, pool.Count);
        Assert.Equal(pinned + retained.CommittedStorageBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        Assert.Equal(8, retained.Count);
        Assert.Equal(1, retained[0]);
        Assert.Equal(64, governor.CurrentReservedBytes);
        governor.ReleaseReserved(64);
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference<PyList> Dropped, PyList? Retained) CreateOldStorage(
        ChargeReclamationPool pool, MemoryGovernor governor, bool retainAnother)
    {
        var dropped = new PyList([1, 2, 3, 4, 5, 6, 7, 8], governor, Span);
        pool.TrackFreshMutable(dropped, dropped.CommittedStorageBytes, Span);
        var retained = retainAnother ? new PyList([1, 2, 3, 4, 5, 6, 7, 8], governor, Span) : null;
        if (retained is not null) pool.TrackFreshMutable(retained, retained.CommittedStorageBytes, Span);
        for (var i = 0; i <= GC.MaxGeneration; i++)
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        Assert.Equal(GC.MaxGeneration, GC.GetGeneration(dropped));
        if (retained is not null) Assert.Equal(GC.MaxGeneration, GC.GetGeneration(retained));
        var weak = new WeakReference<PyList>(dropped);
        GC.KeepAlive(dropped);
        return (weak, retained);
    }

    private static long PinRemaining(MemoryGovernor governor)
    {
        var bytes = 16384 - governor.CurrentAccountedBytes;
        governor.Reserve(bytes, Span);
        governor.Commit(bytes);
        return bytes;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<PyList> target) => target.TryGetTarget(out _);
}
