using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class YoungTierExhaustionTests
{
    [Fact]
    public void NewestLiveEntryCannotBlockCollectedYoungBacklogRelief()
    {
        var governor = new MemoryGovernor(65536);
        var pool = new ChargeReclamationPool(governor);
        governor.LivePoolProvider = () => new[] { pool };
        var retained = RegisterDeadThenLive(governor, pool);
        var pressure = 65536 - governor.CurrentCommittedBytes - 1;
        governor.Reserve(pressure, null);
        var error = Record.Exception(() => governor.Reserve(200, null));
        Assert.True(error is null, $"{error}; denied={governor.LastDeniedReservationBytes}, committed={governor.CurrentCommittedBytes}, reserved={governor.CurrentReservedBytes}, entries={pool.Count}");
        governor.ReleaseReserved(pressure + 200);
        Assert.Equal(1, pool.Count);
        Assert.Equal(256 + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object RegisterDeadThenLive(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var discarded = new object();
        var retained = new object();
        governor.Reserve(256, null);
        governor.Commit(256);
        pool.Track(discarded, 128);
        pool.Track(retained, 128);
        return retained;
    }
}
