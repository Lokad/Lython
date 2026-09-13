using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG01/MG24: the reclamation pool releases charges only for entries whose
/// targets were collected, keeps every live entry charged, and tracks
/// governed strings for their exact construction charge.
/// </summary>
public sealed class ChargeReclamationPoolTests
{
    [Fact]
    public void DeadEntriesReleaseExactCharges()
    {
        // The 100 value bytes ride beside the 64-byte registry charge; both
        // release once the target is collected.
        var governor = new MemoryGovernor(1000000);
        var pool = new ChargeReclamationPool(governor);
        governor.Reserve(100, null);
        governor.Commit(100);
        TrackDeadObject(pool);
        Assert.Equal(1, pool.Count);
        Assert.Equal(164L, governor.CurrentCommittedBytes);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(164L, pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
        Assert.Equal(0L, governor.CurrentReservedBytes);
    }

    [Fact]
    public void LiveEntriesKeepCharges()
    {
        var governor = new MemoryGovernor(1000000);
        var pool = new ChargeReclamationPool(governor);
        governor.Reserve(100, null);
        governor.Commit(100);
        var held = new object();
        pool.Track(held, 100);
        GC.Collect();
        Assert.Equal(0L, pool.Sweep());
        Assert.Equal(1, pool.Count);
        Assert.Equal(164L, governor.CurrentCommittedBytes);
        GC.KeepAlive(held);
    }

    [Fact]
    public void ReplacedStorageResnapshotsWithoutDoubleRelease()
    {
        // A pooled list cleared through the value releases its backing there
        // and re-snapshots the replacement, so a later sweep releases exactly
        // the current backing instead of the stale snapshot.
        var governor = new MemoryGovernor(1000000);
        var pool = new ChargeReclamationPool(governor);
        var (tracked, empty) = ClearTrackedList(pool, governor);
        Assert.True(tracked > empty);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(empty + 64L, pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
        Assert.Equal(0L, governor.CurrentReservedBytes);
    }

    [Fact]
    public void OldTierRevisitsPromotedEntries()
    {
        // Survivors promote to the old tier, which later sweeps revisit in
        // bounded quanta instead of rescanning on a fixed cadence. The live
        // phase runs inside a helper frame so no test slots root the objects
        // once it returns.
        var (pool, governor) = PromoteThreeToOldTier();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(222L, pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ChargeReclamationPool Pool, MemoryGovernor Governor) PromoteThreeToOldTier()
    {
        var governor = new MemoryGovernor(1000000);
        var pool = new ChargeReclamationPool(governor);
        governor.Reserve(30, null);
        governor.Commit(30);
        var first = new object();
        var second = new object();
        var third = new object();
        pool.Track(first, 10);
        pool.Track(second, 10);
        pool.Track(third, 10);
        GC.KeepAlive(first);
        GC.KeepAlive(second);
        GC.KeepAlive(third);
        Assert.Equal(0L, pool.Sweep());
        Assert.Equal(3, pool.Count);
        return (pool, governor);
    }

    [Fact]
    public void TrackStringUsesExactConstructionCharge()
    {
        var governor = new MemoryGovernor(1000000);
        var pool = new ChargeReclamationPool(governor);
        pool.TrackString(PyString.Empty);
        Assert.Equal(0, pool.Count);
        TrackAbcString(pool, governor);
        Assert.Equal(1, pool.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(195L, pool.Sweep());
        Assert.Equal(0, pool.Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long Tracked, long Empty) ClearTrackedList(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var items = new object[20];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = PyString.Empty;
        }

        var list = new PyList(items, governor, null);
        var backing = list.CommittedStorageBytes;
        Assert.True(backing > 0);
        pool.TrackMutable(list, backing);
        list.Clear();
        return (backing, list.CommittedStorageBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackDeadObject(ChargeReclamationPool pool)
        => pool.Track(new object(), 100);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackAbcString(ChargeReclamationPool pool, MemoryGovernor governor)
        => pool.TrackString(PyString.FromString("abc", governor));
}