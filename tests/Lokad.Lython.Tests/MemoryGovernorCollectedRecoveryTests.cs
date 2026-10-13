using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class MemoryGovernorCollectedRecoveryTests
{
    [Fact]
    public void CollectedOwnersAfterPinnedDenialFundNextRequestWithoutAnotherCollection()
    {
        var governor = new MemoryGovernor(336);
        var pool = new ChargeReclamationPool(governor);
        governor.ReclamationPool = pool;
        var target = HoldThroughDenialThenDrop(governor, pool);
        var youngBefore = GC.CollectionCount(0);
        GC.Collect(GC.MaxGeneration);
        Assert.False(target.TryGetTarget(out _));
        Assert.True(GC.CollectionCount(0) > youngBefore);
        var collections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
        governor.EnsureCanReserve(64, null);
        Assert.Equal(collections, Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount));
        Assert.Equal(48, governor.CurrentCommittedBytes);
        Assert.Equal(0, pool.Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> HoldThroughDenialThenDrop(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var value = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        pool.Track(value, 128);
        governor.Reserve(16, null);
        governor.Commit(16);
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(64, null));
        GC.KeepAlive(value);
        return new(value);
    }

    [Fact]
    public void PartialSweepDoesNotClaimAllCollectedOwnersWereReconciled()
    {
        var governor = new MemoryGovernor(1_000_000);
        var pool = new ChargeReclamationPool(governor);
        governor.ReclamationPool = pool;
        HoldInventoryThroughDenialThenDrop(governor, pool);
        GC.Collect(GC.MaxGeneration);
        pool.Sweep();
        Assert.InRange(pool.Count, 1, 4999);
        Assert.True(pool.HasCollectionSinceFullSweep);
        var collections = Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount).ToArray();
        governor.EnsureCanReserve(900_000, null);
        Assert.Equal(collections, Enumerable.Range(0, GC.MaxGeneration + 1).Select(GC.CollectionCount));
        Assert.Equal(0, pool.Count);
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.False(pool.HasCollectionSinceFullSweep);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void HoldInventoryThroughDenialThenDrop(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var values = new object[5000];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = new object();
            governor.Reserve(16, null);
            governor.Commit(16);
            pool.Track(values[i], 16);
        }
        pool.Sweep(full: true);
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(900_000, null));
        GC.KeepAlive(values);
    }
}
