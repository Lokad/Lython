using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class FinalizableOwnershipTests
{
    private const long NativeCoupon = 131072;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingFinalizationRetainsTheCouponInBothPoolTiers(bool promote)
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        using var gates = new FinalizerGates();
        var weak = RegisterDroppedOwner(pool, governor, gates, promote);
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            Assert.True(gates.Started.Wait(TimeSpan.FromSeconds(10)), "Owner did not enter finalization.");
            Assert.False(weak.IsAlive); // Ordinary reachability already ended.
            Assert.Equal(0, pool.Sweep(full: true));
            Assert.Equal(1, pool.Count);
            Assert.Equal(NativeCoupon + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
                governor.CurrentCommittedBytes);
            Assert.Equal(0, governor.CurrentReservedBytes);
        }
        finally
        {
            gates.Release.Set();
            GC.WaitForPendingFinalizers();
        }
        Assert.True(gates.Finished.IsSet);
        // Finishing a finalizer is not permission to mutate the governor.
        Assert.Equal(NativeCoupon + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        Assert.Equal(NativeCoupon + ChargeReclamationPool.EntryChargeBytes, pool.Sweep(full: true));
        Assert.Equal(0, pool.Count);
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        Assert.Equal(0, pool.Sweep(full: true));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterDroppedOwner(ChargeReclamationPool pool, MemoryGovernor governor,
        FinalizerGates gates, bool promote)
    {
        governor.Reserve(NativeCoupon, null);
        governor.Commit(NativeCoupon);
        var owner = new NativeOwner(gates);
        pool.TrackCallResult(owner);
        if (promote) pool.Sweep();
        return new WeakReference(owner);
    }

    private sealed class NativeOwner(FinalizerGates gates) : IPyFinalizableOwnership
    {
        public bool TrySnapshotOwnership(out long chargeBytes) { chargeBytes = NativeCoupon; return true; }
        ~NativeOwner()
        {
            gates.Started.Set();
            gates.Release.Wait(TimeSpan.FromSeconds(30));
            gates.Finished.Set();
        }
    }

    private sealed class FinalizerGates : IDisposable
    {
        public ManualResetEventSlim Started { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public ManualResetEventSlim Finished { get; } = new(false);
        public void Dispose() { Started.Dispose(); Release.Dispose(); Finished.Dispose(); }
    }
}
