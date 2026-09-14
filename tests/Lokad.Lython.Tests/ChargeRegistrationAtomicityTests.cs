// MG03/MG24: registration is a single identity-based mechanism and failure is
// atomic — a denied track publishes nothing, so a funded retry registers.
// ConditionalWeakTable keys by reference identity, so value-equal but distinct
// strings track (and release) independently through the shared path.
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class ChargeRegistrationAtomicityTests
{
    [Fact]
    public void DeniedTrackRetriesAfterFunding()
    {
        // The 950 B value charge fits the 1,000 B budget but leaves no room
        // for the 64 B registry charge, so the first track denies. Funding
        // (standing in for budget freed by dropping other values) must let
        // the same value register instead of hitting a stale mark.
        var governor = new MemoryGovernor(1000);
        var pool = new ChargeReclamationPool(governor);
        var value = new object();
        governor.Reserve(950, null);
        governor.Commit(950);
        var failure = Assert.Throws<LythonRuntimeException>(() => pool.Track(value, 950));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, pool.Count);
        governor.Release(950);
        pool.Track(value, 950);
        Assert.Equal(1, pool.Count);
        Assert.Equal(64L, governor.CurrentCommittedBytes);
        GC.KeepAlive(value);
    }

    [Fact]
    public void DeniedStringTrackRetriesAfterFunding()
    {
        // A 3-byte string commits 131 B, which fits 150 B but leaves no room
        // for the registry charge. The funded retry registers exactly once.
        var governor = new MemoryGovernor(150);
        var pool = new ChargeReclamationPool(governor);
        var value = PyString.FromString("abc", governor);
        Assert.Throws<LythonRuntimeException>(() => pool.TrackString(value));
        Assert.Equal(0, pool.Count);
        governor.Release(131);
        pool.TrackString(value);
        Assert.Equal(1, pool.Count);
        Assert.Equal(64L, governor.CurrentCommittedBytes);
        GC.KeepAlive(value);
    }

    [Fact]
    public void EqualButDistinctStringsTrackIndependently()
    {
        // Two value-equal strings are distinct identities: both track, and
        // dropping one releases exactly its own value plus registry charge.
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var kept = TrackDistinctPair(pool, governor);
        Assert.Equal(2, pool.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(131L + 64L, pool.Sweep());
        Assert.Equal(1, pool.Count);
        Assert.Equal(131L + 64L, governor.CurrentCommittedBytes);
        GC.KeepAlive(kept);
    }

    [Fact]
    public void RepeatedAliasTracksOnce()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var value = new object();
        governor.Reserve(100, null);
        governor.Commit(100);
        pool.Track(value, 100);
        pool.Track(value, 100);
        Assert.Equal(1, pool.Count);
        Assert.Equal(164L, governor.CurrentCommittedBytes);
        GC.KeepAlive(value);
    }

    [Fact]
    public void CollectedValueReleasesOnSweep()
    {
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
    public void StorageReplacementReleasesCurrentBacking()
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static PyString TrackDistinctPair(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var first = PyString.FromString("abc", governor);
        pool.TrackString(first);
        pool.TrackString(PyString.FromString("abc", governor));
        return first;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackDeadObject(ChargeReclamationPool pool)
        => pool.Track(new object(), 100);

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
}