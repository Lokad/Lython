using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class ReclamationSweepFundingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlreadyFundedLiveEntriesSweepWithoutAdmissionOrAllocation(bool full)
    {
        var governor = new MemoryGovernor(305);
        using var other = governor.ReserveTemporary(17, null);
        var pool = new ChargeReclamationPool(governor);
        var value = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        pool.Track(value, 128);
        Assert.Equal(305, governor.CurrentAccountedBytes);

        // No headroom remains for bookkeeping. A sweep of funded live storage
        // must not need a new admission, disturb another lease or allocate.
        Assert.Equal(0, pool.Sweep(full));
        var before = GC.GetAllocatedBytesForCurrentThread();
        long released = 0;
        for (var i = 0; i < 32; i++) released += pool.Sweep(full);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.Equal(0, released);
        Assert.Equal(1, pool.Count);
        Assert.True(pool.IsTracked(value));
        Assert.Equal(288, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        Assert.Equal(305, governor.PeakAccountedBytes);
        GC.KeepAlive(value);
    }

    [Fact]
    public void RecentDeadEntryReclaimsBehindRetainedPopulation()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var retained = Enumerable.Range(0, 5000).Select(_ => new object()).ToArray();
        foreach (var value in retained)
        {
            governor.Reserve(128, null);
            governor.Commit(128);
            pool.Track(value, 128);
        }
        pool.Sweep();
        var dead = RegisterDropped(governor, pool);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dead.TryGetTarget(out _));
        Assert.Equal(256, pool.Sweep());
        Assert.Equal(5000, pool.Count);
        Assert.Equal(5000 * 256L + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [Theory]
    [InlineData(100, 5000)]
    [InlineData(4500, 5000)]
    [InlineData(8191, 17000)]
    [InlineData(8192, 17000)]
    [InlineData(16384, 17000)]
    public void RefundingUnpublishedRetainedEntryKeepsRecentReclamationExact(int removedIndex, int count)
    {
        var governor = new MemoryGovernor(null);
        using var other = governor.ReserveTemporary(17, null);
        var pool = new ChargeReclamationPool(governor);
        var retained = Enumerable.Range(0, count).Select(_ => new object()).ToArray();
        foreach (var value in retained)
        {
            governor.Reserve(128, null);
            governor.Commit(128);
            pool.Track(value, 128);
        }
        pool.Sweep();
        pool.RefundUnpublishedValue(retained[removedIndex]);
        Assert.False(pool.IsTracked(retained[removedIndex]));
        var dropped = RegisterDropped(governor, pool);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dropped.TryGetTarget(out _));
        Assert.Equal(256, pool.Sweep());
        Assert.Equal(count - 1, pool.Count);
        Assert.Equal((count - 1) * 256L + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        for (var i = 0; i < retained.Length; i++) Assert.Equal(i != removedIndex, pool.IsTracked(retained[i]));
        GC.KeepAlive(retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LargeRetainedPopulationUsesFundedSmallPagesAndAllocationFreeSweeps(bool full)
    {
        var governor = new MemoryGovernor(5 * 1024 * 1024);
        var pool = new ChargeReclamationPool(governor);
        var retained = Enumerable.Range(0, 17000).Select(_ => new object()).ToArray();
        foreach (var value in retained)
        {
            governor.Reserve(128, null);
            governor.Commit(128);
            pool.Track(value, 128);
        }
        using var pressure = governor.ReserveTemporary(governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes, null);
        Assert.Equal(governor.MaxAccountedBytes, governor.CurrentAccountedBytes);
        Assert.Equal(139296, pool.CommittedBackingBytes);
        var pages = (List<List<ChargeReclamationPool.ReclamationEntry>>)typeof(ChargeReclamationPool)
            .GetField("_pages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(pool)!;
        Assert.All(pages, page => Assert.InRange(page.Capacity, 1, 8192));
        Assert.Equal(8L * (pages.Sum(page => page.Capacity) + pages.Capacity), pool.CommittedBackingBytes);
        Assert.Equal(0, pool.Sweep(full));
        var before = GC.GetAllocatedBytesForCurrentThread();
        long released = 0;
        for (var i = 0; i < 32; i++) released += pool.Sweep(full);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(0, released);
        Assert.Equal(retained.Length, pool.Count);
        Assert.Equal(retained.Length * 256L + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(retained);
    }

    [Fact]
    public void NewPageAndRegistryGrowthDenyAtomicallyAndRetryWithExactFunding()
    {
        const int count = 32768;
        const long backing = count * 8L + 32;
        var governor = new MemoryGovernor(count * 256L + backing + 128 + 128 + 17 + 4096);
        var pool = new ChargeReclamationPool(governor);
        var retained = Enumerable.Range(0, count).Select(_ => new object()).ToArray();
        foreach (var value in retained)
        {
            governor.Reserve(128, null);
            governor.Commit(128);
            pool.Track(value, 128);
        }
        var fresh = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        using var other = governor.ReserveTemporary(17, null);
        using var pressure = governor.ReserveTemporary(4096, null);
        var before = governor.CurrentCommittedBytes;
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => pool.Track(fresh, 128)).ExceptionType);
        Assert.Equal(64, governor.LastDeniedReservationBytes);
        Assert.Equal(before, governor.CurrentCommittedBytes);
        Assert.Equal(4113, governor.CurrentReservedBytes);
        Assert.Equal(count, pool.Count);
        Assert.False(pool.IsTracked(fresh));
        pressure.Dispose();
        pool.Track(fresh, 128);
        Assert.Equal(count + 1, pool.Count);
        Assert.True(pool.IsTracked(fresh));
        Assert.Equal(backing + 64, pool.CommittedBackingBytes);
        Assert.Equal(before + 192, governor.CurrentCommittedBytes);
        Assert.Equal(17, governor.CurrentReservedBytes);
        GC.KeepAlive(retained);
        GC.KeepAlive(fresh);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> RegisterDropped(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var value = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        pool.Track(value, 128);
        return new(value);
    }
}
