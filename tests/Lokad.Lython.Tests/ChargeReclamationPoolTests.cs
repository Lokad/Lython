using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// PERF01: old-tier sweeps visit each entry at most once per sweep up to the
// quantum (no wrap-around revisits), and exhaustion relief skips collections
// it cannot benefit from. Cursor/contents below are observed by reflection;
// committed bytes prove release math.
[Collection("AllocationSensitive")]
public sealed class ChargeReclamationPoolTests
{
    private static ChargeReclamationPool NewPool(MemoryGovernor governor)
        => new ChargeReclamationPool(governor);

    private static int OldCursor(ChargeReclamationPool pool)
        => (int)typeof(ChargeReclamationPool).GetField("_sweepCursor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!;

    private static int OldCount(ChargeReclamationPool pool)
    {
        return (int)typeof(ChargeReclamationPool).GetField("_recentStart", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pool)!;
    }

    private static List<object> TrackLive(ChargeReclamationPool pool, MemoryGovernor governor, int count)
    {
        var keys = new List<object>();
        for (var i = 0; i < count; i++)
        {
            keys.Add(new object());
        }

        for (var i = 0; i < keys.Count; i++)
        {
            governor.Reserve(128, null);
            governor.Commit(128);
            pool.Track(keys[i], 128);
        }

        return keys;
    }

    [Fact]
    public void EmptyPoolSweepIsNoop()
    {
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        Assert.Equal(0, pool.Sweep());
        Assert.Equal(0, OldCursor(pool));
    }

    [Fact]
    public void TinyTierWrapsToStart()
    {
        // A complete pass wraps the cursor to the start: with one live entry
        // the first sweep promotes it and visits it, ending back at zero.
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 1);
        pool.Sweep();
        Assert.Equal(1, OldCount(pool));
        Assert.Equal(0, OldCursor(pool));
        pool.Sweep();
        Assert.Equal(0, OldCursor(pool));
        Assert.Equal(1, OldCount(pool));
        GC.KeepAlive(keys);
    }

    [Fact]
    public void LargeTierAdvancesExactlyOneQuantum()
    {
        // The first sweep promotes the young tier then visits the first
        // quantum of the old tier; later sweeps advance one quantum each and
        // wrap only on completion (the old code wrapped mid-sweep and
        // revisited entries).
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 5000);
        pool.Sweep();
        Assert.Equal(5000, OldCount(pool));
        Assert.Equal(4096, OldCursor(pool));
        pool.Sweep();
        Assert.Equal(0, OldCursor(pool));
        Assert.Equal(5000, pool.Count);
        pool.Sweep();
        Assert.Equal(4096, OldCursor(pool));
        GC.KeepAlive(keys);
    }

    [Fact]
    public void ShrinkingTierReleasesExactDead()
    {
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 100);
        pool.Sweep();
        Assert.Equal(100, OldCount(pool));
        DropHalf(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var released = pool.Sweep();
        Assert.Equal(50 * (128 + 128), released);
        Assert.Equal(50, pool.Count);
        GC.KeepAlive(keys);
    }

    [Fact]
    public void MixedTierReleasesDeadAndKeepsLive()
    {
        // Scattered dead entries release exactly once across quantum windows:
        // removal compacts from the end, which can only pull unvisited entries
        // into the window, so live entries are never lost or double-visited.
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 5000);
        pool.Sweep();
        Assert.Equal(5000, OldCount(pool));
        Assert.Equal(4096, OldCursor(pool));
        DropOdd(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var released = 0L;
        var sweeps = 0;
        while (pool.Count > 2500 && sweeps < 10)
        {
            released += pool.Sweep();
            sweeps++;
        }

        Assert.Equal(2500 * (128 + 128), released);
        Assert.Equal(2500, pool.Count);
        GC.KeepAlive(keys);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void DropHalf(List<object> keys)
    {
        keys.RemoveRange(0, 50);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void DropOdd(List<object> keys)
    {
        for (var i = 1; i < keys.Count; i += 2)
        {
            keys[i] = null!;
        }
    }

    [Fact]
    public void ImpossibleRequestDeniesWithoutSweeping()
    {
        // A request larger than the whole budget can never fit, so it denies
        // without pausing for a collection: the live pool is untouched.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 2048 });
        var keys = TrackLive(context.State.CallTemporaries, context.MemoryGovernor, 4);
        context.State.CallTemporaries.Sweep();
        var before = context.State.CallTemporaries.Count;
        var failure = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.EnsureCanReserve(4096, null));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Equal(before, context.State.CallTemporaries.Count);
        GC.KeepAlive(keys);
    }

    [Fact]
    public void EmptyPoolsSkipCollectionRelief()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 1024 });
        context.MemoryGovernor.Reserve(1024, null);
        context.MemoryGovernor.Commit(1024);
        Assert.Equal(0, context.State.CallTemporaries.Count);
        var failure = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.EnsureCanReserve(64, null));
        Assert.Equal("MemoryError", failure.ExceptionType);
    }

    [Fact]
    public void TierGrowthCommitsPerSlot()
    {
        // Five insertions grow the young tier 0->4->8 (deltas 4 + 4): 8 slots beside the entries.
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 5);
        Assert.Equal(8L * 8L, pool.CommittedBackingBytes);
        Assert.Equal(5L * (128L + 128L) + 8L * 8L, governor.CurrentCommittedBytes);
        GC.KeepAlive(keys);
    }

    [Fact]
    public void RefillReusesRetainedCapacity()
    {
        // A partial sweep prunes dead entries but keeps tier capacity: refilling
        // within it commits no new backing.
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 4);
        var backing = pool.CommittedBackingBytes;
        DropAll(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        pool.Sweep();
        Assert.Equal(backing, pool.CommittedBackingBytes);
        Assert.Equal(backing, governor.CurrentCommittedBytes);
        keys = TrackLive(pool, governor, 4);
        Assert.Equal(backing, pool.CommittedBackingBytes);
        Assert.Equal(4L * (128L + 128L) + backing, governor.CurrentCommittedBytes);
        GC.KeepAlive(keys);
    }


    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void DropAll(System.Collections.Generic.List<object> keys)
    {
        keys.Clear();
    }
    [Fact]
    public void OldTierDeadRemovalObeysQuantum()
    {
        // 9,000 promoted entries then all dropped: the first two sweeps visit
        // one quantum each, the third wraps after a partial window, and later
        // sweeps drain exactly to zero.
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 9000);
        pool.Sweep();
        Assert.Equal(9000, OldCount(pool));
        Assert.Equal(4096, OldCursor(pool));
        pool.Sweep();
        Assert.Equal(9000, OldCount(pool));
        Assert.Equal(8192, OldCursor(pool));
        DropAll(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var released = pool.Sweep();
        Assert.Equal(808 * (128 + 128), released);
        Assert.Equal(8192, OldCount(pool));
        Assert.Equal(0, OldCursor(pool));
        // Exactly one quantum dies per sweep from here: the pre-cap code drained
        // all 8,192 remaining entries in the next sweep.
        Assert.Equal(4096 * (128 + 128), pool.Sweep());
        Assert.Equal(4096, pool.Count);
        Assert.Equal(0, OldCursor(pool));
        Assert.Equal(4096 * (128 + 128), pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0, OldCursor(pool));
        Assert.Equal(9000 * (128 + 128), released + 2 * 4096 * (128 + 128));
        GC.KeepAlive(keys);
    }

    [Fact]
    public void MostlyDeadTierCapsProbesPerSweep()
    {
        // 5,000 promoted entries with 100 scattered survivors: one ordinary sweep
        // cannot release all 4,900 dead charges (at most one 4,096-probe window dies
        // per sweep), but repeated sweeps drain exactly to the survivors.
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 5000);
        pool.Sweep();
        Assert.Equal(5000, OldCount(pool));
        Assert.Equal(4096, OldCursor(pool));
        DropAllButEveryFiftieth(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var released = pool.Sweep();
        Assert.True(released > 0);
        Assert.True(released < 4900 * (128 + 128));
        Assert.Equal(0, released % (128 + 128));
        var total = released;
        var guard = 0;
        while (pool.Count > 100 && guard < 10)
        {
            total += pool.Sweep();
            guard++;
        }

        Assert.Equal(4900 * (128 + 128), total);
        Assert.Equal(100, pool.Count);
        GC.KeepAlive(keys);
    }

    [Fact]
    public void TinyDeadTierReleasesExactly()
    {
        var governor = new MemoryGovernor(null);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 3);
        pool.Sweep();
        Assert.Equal(3, OldCount(pool));
        DropAll(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(3 * (128 + 128), pool.Sweep());
        Assert.Equal(0, pool.Count);
        Assert.Equal(0, OldCursor(pool));
        GC.KeepAlive(keys);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void DropAllButEveryFiftieth(List<object> keys)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (i % 50 != 0)
            {
                keys[i] = null!;
            }
        }
    }

    [Fact]
    public void ReliefSweepBeforeCollectCoversShortfall()
    {
        // 300 retained entries then all dropped and collected but unswept: the
        // denied reservation triggers relief, whose pre-collection sweep frees the
        // dead entries and covers the shortfall, so the reservation succeeds with
        // only pre-existing backing left committed. Tier backing is not asserted
        // literally: pool residency at context setup shifts the growth sequence. Committed math proves the sweep freed
        // exactly the dead charges; collection counts stay unpinned because xunit
        // runs suites in parallel and process-wide GC observations flake.
        // The pool must be registered for relief: a standalone pool is invisible
        // to the provider, so this runs on a real execution state like other
        // exhaustion tests.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 100000 });
        var governor = context.MemoryGovernor;
        var pool = context.State.CallTemporaries;
        var baseline = governor.CurrentCommittedBytes;
        var keys = TrackLive(pool, governor, 300);
        var backing = pool.CommittedBackingBytes;
        Assert.Equal(baseline + 300L * (128L + 128L) + backing, governor.CurrentCommittedBytes);
        DropAll(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        governor.Reserve(50000, null);
        Assert.Equal(50000, governor.CurrentReservedBytes);
        Assert.Equal(baseline + backing, governor.CurrentCommittedBytes);
        GC.KeepAlive(keys);
    }


    [Fact]
    public void ReliefReconcilesPartialCollectionBeforeFullyReclaimingDroppedTargets()
    {
        // Former promotion bookkeeping could deny before exhaustion relief.
        // Retain that original envelope. An already-collected subset can now
        // cover the shortfall without forcing collection of every dropped target;
        // check exact residual accounting, then fully reconcile after controlled GC.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 2000000 });
        var governor = context.MemoryGovernor;
        var pool = context.State.CallTemporaries;
        var baseline = governor.CurrentCommittedBytes;
        var oldKeys = TrackLive(pool, governor, 4096);
        pool.Sweep();
        Assert.Equal(4096, OldCount(pool));
        var youngKeys = TrackLive(pool, governor, 100);
        DropAll(oldKeys);
        DropAll(youngKeys);
        using var headroom = governor.ReserveTemporary(0, null);
        var hold = 2000000L - governor.CurrentAccountedBytes - 15000L;
        Assert.True(hold > 0, "setup overran its budget envelope");
        headroom.Grow(hold, null);
        governor.Reserve(25000, null);
        Assert.Equal(hold + 25000, governor.CurrentReservedBytes);
        Assert.True(governor.CurrentAccountedBytes <= 2000000);
        Assert.Equal(baseline + pool.CommittedBackingBytes + pool.Count * 256L, governor.CurrentCommittedBytes);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        pool.Sweep(full: true);
        Assert.Equal(0, pool.Count);
        Assert.Equal(baseline + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(oldKeys);
        GC.KeepAlive(youngKeys);
    }

    [Fact]
    public void PartialReliefKeepsOtherRetainedTargetsFunded()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 2000000 });
        var governor = context.MemoryGovernor;
        var pool = context.State.CallTemporaries;
        var baseline = governor.CurrentCommittedBytes;
        var dropped = TrackLive(pool, governor, 100);
        var retained = TrackLive(pool, governor, 100);
        DropAll(dropped);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var hold = governor.ReserveTemporary(2000000 - governor.CurrentAccountedBytes - 15000, null);
        governor.Reserve(25000, null);
        Assert.Equal(100, pool.Count);
        Assert.Equal(baseline + pool.CommittedBackingBytes + 25600, governor.CurrentCommittedBytes);
        DropAll(retained);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        pool.Sweep(full: true);
        Assert.Equal(0, pool.Count);
        Assert.Equal(baseline + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        governor.ReleaseReserved(25000);
        GC.KeepAlive(dropped);
        GC.KeepAlive(retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedRecentSweepReleasesCollectedEntriesAtTheCap(bool full)
    {
        // The two entries and backing exactly fill the cap. Releasing the dead
        // entry must preserve the live one without admitting more bookkeeping.
        var governor = new MemoryGovernor(544);
        var pool = NewPool(governor);
        var keys = TrackLive(pool, governor, 2);
        DropLast(keys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Equal(256, pool.Sweep(full));
        Assert.Equal(1, pool.Count);
        Assert.Equal(1, OldCount(pool));
        Assert.Equal(288, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.LastDeniedReservationBytes);
        Assert.Equal(544, governor.PeakAccountedBytes);
        GC.KeepAlive(keys);
    }

    [Fact]
    public void MixedRecentSweepNeedsNoPromotionAndRefundsExactly()
    {
        // The baseline needed promotion growth here and denied. The existing
        // single array already funds every entry; a sweep needs no new budget.
        var governor = new MemoryGovernor(17920);
        var pool = NewPool(governor);
        var oldKeys = TrackLive(pool, governor, 64);
        pool.Sweep();
        var youngKeys = TrackLive(pool, governor, 2);
        DropLast(youngKeys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.Equal(256, pool.Sweep());
        Assert.Equal(65, pool.Count);
        Assert.Equal(65, OldCount(pool));
        Assert.Equal(17664, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(0, governor.LastDeniedReservationBytes);

        DropAll(oldKeys);
        DropAll(youngKeys);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(65 * 256, pool.Sweep(full: true));
        Assert.Equal(0, pool.Count);
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.Equal(1024, governor.CurrentCommittedBytes);
        GC.KeepAlive(oldKeys);
        GC.KeepAlive(youngKeys);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void DropLast(List<object> keys)
    {
        keys[^1] = null!;
    }

    [Fact]
    public void InstanceAttributeCouponTracksSetsAndRemovals()
    {
        // CommittedAttributeBytes mirrors live slots exactly: adopting through
        // TrackGrowth snapshots the total, later sets and removals re-snapshot,
        // and dropping releases exactly the coupon plus one entry charge.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var governor = context.MemoryGovernor;
        var pool = context.State.CallTemporaries;
        var baseline = governor.CurrentCommittedBytes;
        var entriesBefore = pool.Count;
        TrackInstanceWithTwoAttrsThenDropOne(pool, governor, entriesBefore);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        pool.Sweep();
        Assert.Equal(entriesBefore, pool.Count);
        Assert.Equal(baseline + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void TrackInstanceWithTwoAttrsThenDropOne(ChargeReclamationPool pool, MemoryGovernor governor, int entriesBefore)
    {
        var type = new PyType("R", [], new System.Collections.Generic.Dictionary<string, object>());
        var instance = new PyInstance(type, governor, null);
        instance.SetAttribute("a", 1);
        Assert.Equal(64, instance.CommittedAttributeBytes);
        pool.TrackGrowth(instance, instance.CommittedStorageBytes, null);
        Assert.Equal(entriesBefore + 1, pool.Count);
        instance.SetAttribute("b", 2);
        instance.RemoveAttribute("a");
        Assert.Equal(64, instance.CommittedAttributeBytes);
        Assert.Equal(entriesBefore + 1, pool.Count);
    }
}
