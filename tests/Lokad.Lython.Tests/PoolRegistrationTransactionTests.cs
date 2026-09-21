using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// R08/R09: pool registration is fully transactional and sweeps release exactly
// on every exceptional exit. Each test pins deterministic governor/pool numbers
// (not GC peak slopes) and runs under both Debug (strict pairing) and Release.
public sealed class PoolRegistrationTransactionTests
{
    // R08 PLAN repro: 129 B funds a 1 B value plus the 128 B entry, but not the
    // 32 B initial tier growth. The denial must strand no reserved bytes and
    // refund the orphaned fresh value.
    [Fact]
    public void FirstInsertionTierGrowthDenialStrandsNothing()
    {
        var governor = new MemoryGovernor(129);
        var pool = new ChargeReclamationPool(governor);
        var value = new object();
        governor.Reserve(1, null);
        governor.Commit(1);
        var failure = Assert.Throws<LythonRuntimeException>(() => pool.TrackFreshMutable(value, 1));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, pool.Count);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(value);
    }

    [Fact]
    public void DeniedFreshValueLeavesNoMarkForFundedRetry()
    {
        var governor = new MemoryGovernor(129);
        var pool = new ChargeReclamationPool(governor);
        var value = new object();
        governor.Reserve(1, null);
        governor.Commit(1);
        Assert.Throws<LythonRuntimeException>(() => pool.TrackFreshMutable(value, 1));
        Assert.Equal(0, pool.Count);

        // No publication happened, so the same identity registers cleanly once funded.
        var funded = new MemoryGovernor(null);
        var fundedPool = new ChargeReclamationPool(funded);
        funded.Reserve(1, null);
        funded.Commit(1);
        fundedPool.TrackFreshMutable(value, 1);
        Assert.Equal(1, fundedPool.Count);
        GC.KeepAlive(value);
    }

    [Fact]
    public void DeniedFreshStringAtTierGrowthRefundsConstruction()
    {
        // 131 B construction + 128 B entry fit 260 B, but the 32 B initial tier
        // growth does not: the orphaned string refunds exactly.
        var governor = new MemoryGovernor(260);
        var pool = new ChargeReclamationPool(governor);
        var value = PyString.FromString("abc", governor);
        var failure = Assert.Throws<LythonRuntimeException>(() => pool.TrackFreshString(value));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, pool.Count);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(value);
    }

    [Fact]
    public void LaterCapacityGrowthDenialRollsBackAndStaysStable()
    {
        // Four entries fill the young tier (0->4, one 32 B backing charge).
        // The fifth needs 4->8 growth: budget fits its value plus the entry but
        // stops 1 B short of the growth charge.
        var governor = new MemoryGovernor(753);
        var pool = new ChargeReclamationPool(governor);
        var keys = new List<object>();
        for (var i = 0; i < 4; i++)
        {
            keys.Add(new object());
        }

        foreach (var key in keys)
        {
            governor.Reserve(10, null);
            governor.Commit(10);
            pool.Track(key, 10);
        }

        Assert.Equal(4, pool.Count);
        Assert.Equal(584, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);

        var fifth = new object();
        governor.Reserve(10, null);
        governor.Commit(10);
        var failure = Assert.Throws<LythonRuntimeException>(() => pool.Track(fifth, 10));
        Assert.Equal("MemoryError", failure.ExceptionType);
        // Plain registration never refunds the caller-owned value charge, but the
        // operation's own entry and growth reservations roll back exactly.
        Assert.Equal(4, pool.Count);
        Assert.Equal(594, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);

        // Repeating the denial changes nothing further.
        var retry = Assert.Throws<LythonRuntimeException>(() => pool.Track(fifth, 10));
        Assert.Equal("MemoryError", retry.ExceptionType);
        Assert.Equal(4, pool.Count);
        Assert.Equal(594, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);

        governor.Release(10);
        Assert.Equal(584, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(keys);
        GC.KeepAlive(fifth);
    }

    [Fact]
    public void AliasedTrackReservesNothing()
    {
        var governor = new MemoryGovernor(228 + 32);
        var pool = new ChargeReclamationPool(governor);
        var value = new object();
        governor.Reserve(100, null);
        governor.Commit(100);
        pool.Track(value, 100);
        // No headroom remains, yet re-tracking the same identity is a no-op.
        pool.Track(value, 100);
        Assert.Equal(1, pool.Count);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(value);
    }

    [Fact]
    public void GetObjectIdSecondStageDenialIsAtomic()
    {
        // 64 B identity commit + 128 B entry fit 200 B, but the 32 B initial tier
        // growth does not: the whole identity registration rolls back.
        var root = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 200 });
        var governor = root.MemoryGovernor;
        var failure = Assert.Throws<LythonRuntimeException>(() => root.State.GetObjectId(new object()));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, root.State.CallTemporaries.Count);
    }

    [Fact]
    public void GetObjectIdPublishesIdentityOnceFunded()
    {
        var root = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = null });
        var key = new object();
        var first = root.State.GetObjectId(key);
        Assert.Equal(first, root.State.GetObjectId(key));
        Assert.Equal(1, root.State.CallTemporaries.Count);
        GC.KeepAlive(key);
    }

    // R09: one live and one dead 100 B value (2 x (100 + 128) + 32 backing = 488),
    // plus 16 B filler on a 520 B budget. The sweep removes the dead entry before
    // the 32 B live promotion denies; the removed charges must still release and
    // the live entry stays queued for retry.
    [Fact]
    public void SweepPromotionDenialReleasesDeadAndKeepsLive()
    {
        var governor = new MemoryGovernor(520);
        var pool = new ChargeReclamationPool(governor);
        var live = new object();
        governor.Reserve(100, null);
        governor.Commit(100);
        pool.Track(live, 100);
        TrackDeadValue(pool, governor);
        Assert.Equal(2, pool.Count);
        Assert.Equal(488, governor.CurrentCommittedBytes);
        governor.Reserve(16, null);
        governor.Commit(16);
        Assert.Equal(504, governor.CurrentCommittedBytes);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var failure = Assert.Throws<LythonRuntimeException>(() => pool.Sweep());
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(1, pool.Count);
        Assert.Equal(276, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);

        // A second denial without funding strands nothing further.
        governor.Reserve(244, null);
        governor.Commit(244);
        Assert.Equal(520, governor.CurrentCommittedBytes);
        var again = Assert.Throws<LythonRuntimeException>(() => pool.Sweep());
        Assert.Equal("MemoryError", again.ExceptionType);
        Assert.Equal(1, pool.Count);
        Assert.Equal(520, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        governor.Release(244);

        // Funded retry promotes the survivor: 100 value + 128 entry + 64 backing.
        Assert.Equal(276, governor.CurrentCommittedBytes);
        governor.Release(16);
        Assert.Equal(260, governor.CurrentCommittedBytes);
        Assert.Equal(0, pool.Sweep(full: true));
        Assert.Equal(1, pool.Count);
        Assert.Equal(292, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        GC.KeepAlive(live);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TrackDeadValue(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        governor.Reserve(100, null);
        governor.Commit(100);
        pool.Track(new object(), 100);
    }
}
