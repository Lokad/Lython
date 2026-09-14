using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

// MG03/MG04: factory snapshots mirror committed charges, so drops release
// exactly; wholesale replacement re-snapshots through the value, so clears
// release only the live remainder instead of the stale backing.
public sealed class FactoryStorageAccountingTests
{
    [Fact]
    public void DroppedTupleReleasesExactBacking()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var snapshot = TrackDroppedTuple(pool, governor);
        Assert.True(snapshot > 0);
        Assert.Equal(1, pool.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(snapshot + 64L, pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void ClearedListReleasesEmptyBacking()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var (snapshot, cleared) = TrackClearedList(pool, governor);
        Assert.True(snapshot > cleared);
        Assert.Equal(1, pool.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(cleared + 64L, pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void ClearedDictReleasesEmptyBacking()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var (snapshot, cleared) = TrackClearedDict(pool, governor);
        Assert.True(snapshot > cleared);
        Assert.Equal(1, pool.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(cleared + 64L, pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long TrackDroppedTuple(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var tuple = new PyTuple(new object[] { 1, 2, 3 }, governor, null);
        var snapshot = tuple.CommittedStorageBytes;
        pool.TrackMutable(tuple, snapshot);
        return snapshot;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long Snapshot, long Cleared) TrackClearedList(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var items = new object[20];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = i;
        }

        var list = new PyList(items, governor, null);
        var snapshot = list.CommittedStorageBytes;
        pool.TrackMutable(list, snapshot);
        list.Clear();
        return (snapshot, list.CommittedStorageBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long Snapshot, long Cleared) TrackClearedDict(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var dict = new PyDict(governor, null);
        for (var i = 0; i < 20; i++)
        {
            dict.SetItem(Lokad.Lython.Runtime.Text.PyString.FromString("k" + i), i);
        }
        var snapshot = dict.CommittedStorageBytes;
        pool.TrackMutable(dict, snapshot);
        dict.Clear();
        return (snapshot, dict.CommittedStorageBytes);
    }
}