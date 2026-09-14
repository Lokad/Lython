using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

// MG03/MG04: set snapshots own shell plus backing; Clear re-snapshots through
// the value so a later sweep releases exactly the live charges (a stale
// coupon would over-release the cleared backing).
public sealed class SetStorageAccountingTests
{
    [Fact]
    public void DroppedSetReleasesShellAndBacking()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var snapshot = TrackDroppedSet(pool, governor, clear: false);
        Assert.True(snapshot > 128L);
        Assert.Equal(1, pool.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(snapshot + 64L, pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void ClearedSetReleasesShellOnly()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var snapshot = TrackDroppedSet(pool, governor, clear: true);
        Assert.True(snapshot > 128L);
        Assert.Equal(1, pool.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(128L + 64L, pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void DeniedFreshTrackRefundsSnapshot()
    {
        // A 128 B shell fits 150 B but leaves no room for the registry charge:
        // the denial refunds the shell, strands nothing, and registers nothing,
        // so a funded retry behaves like a construction-time denial.
        var governor = new MemoryGovernor(150);
        var pool = new ChargeReclamationPool(governor);
        var set = new PySet(governor, null);
        Assert.Equal(128L, set.CommittedStorageBytes);
        var failure = Assert.Throws<LythonRuntimeException>(() => pool.TrackFreshMutable(set, set.CommittedStorageBytes));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, pool.Count);
        Assert.Equal(0L, governor.CurrentCommittedBytes);
        GC.KeepAlive(set);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long TrackDroppedSet(ChargeReclamationPool pool, MemoryGovernor governor, bool clear)
    {
        var set = new PySet(governor, null);
        set.Add(new BigInteger(1));
        set.Add(new BigInteger(2));
        set.Add(new BigInteger(3));
        var snapshot = set.CommittedStorageBytes;
        pool.TrackMutable(set, snapshot);
        if (clear)
        {
            set.Clear();
        }

        return snapshot;
    }
}