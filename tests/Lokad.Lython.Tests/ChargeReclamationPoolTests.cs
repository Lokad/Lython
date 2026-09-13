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
        var governor = new MemoryGovernor(1000000);
        var pool = new ChargeReclamationPool(governor);
        governor.Reserve(100, null);
        governor.Commit(100);
        TrackDeadObject(pool);
        Assert.Equal(1, pool.Count);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(100L, pool.Sweep());
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
        Assert.Equal(100L, governor.CurrentCommittedBytes);
        GC.KeepAlive(held);
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
        Assert.Equal(131L, pool.Sweep());
        Assert.Equal(0, pool.Count);
    }

    // Allocated out of line so no stack roots survive into the collection;
    // Debug JITs otherwise extend local lifetimes past their last use.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackDeadObject(ChargeReclamationPool pool)
        => pool.Track(new object(), 100);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackAbcString(ChargeReclamationPool pool, MemoryGovernor governor)
        => pool.TrackString(PyString.FromString("abc", governor));
}