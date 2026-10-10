using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class TupleReclamationRegistrationTests
{
    [Fact]
    public void AliasesShareRegistrationWhileEqualTuplesRemainIndependent()
    {
        var governor = new MemoryGovernor(null);
        var first = new ChargeReclamationPool(governor);
        var second = new ChargeReclamationPool(governor);
        var original = new PyTuple(new object[] { PyNone.Instance, PyNone.Instance }, governor);
        first.TrackMutable(original, original.CommittedStorageBytes);
        var charged = governor.CurrentCommittedBytes;
        second.TrackMutable(original, original.CommittedStorageBytes);
        Assert.Equal(charged, governor.CurrentCommittedBytes);
        Assert.Equal(1, first.Count);
        Assert.Equal(0, second.Count);

        var equal = new PyTuple(new object[] { PyNone.Instance, PyNone.Instance }, governor);
        Assert.False(second.IsTracked(equal));
        second.TrackMutable(equal, equal.CommittedStorageBytes);
        Assert.Equal(1, second.Count);
        Assert.Equal(original.CommittedStorageBytes + equal.CommittedStorageBytes
            + 2 * ChargeReclamationPool.EntryChargeBytes + first.CommittedBackingBytes + second.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        GC.KeepAlive(original);
        GC.KeepAlive(equal);
    }

    [Fact]
    public void DeniedRegistrationKeepsRetainedTupleRetryable()
    {
        const long construction = 64;
        var governor = new MemoryGovernor(construction + ChargeReclamationPool.EntryChargeBytes + 32);
        var pool = new ChargeReclamationPool(governor);
        var tuple = new PyTuple(new object[] { PyNone.Instance, PyNone.Instance }, governor);
        governor.Reserve(1, null);
        var failure = Assert.Throws<LythonRuntimeException>(() => pool.TrackMutable(tuple, tuple.CommittedStorageBytes));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.False(pool.IsTracked(tuple));
        Assert.Equal(0, pool.Count);
        Assert.Equal(1, governor.CurrentReservedBytes);
        Assert.Equal(construction, governor.CurrentCommittedBytes);

        governor.ReleaseReserved(1);
        pool.TrackMutable(tuple, tuple.CommittedStorageBytes);
        Assert.True(pool.IsTracked(tuple));
        Assert.Equal(1, pool.Count);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(governor.MaxAccountedBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(tuple);
    }

    [Fact]
    public void RefundedUnpublishedTupleCanRegisterAgainWithoutDoubleRelease()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var tuple = new PyTuple(new object[] { PyNone.Instance, PyNone.Instance }, governor);
        pool.TrackMutable(tuple, tuple.CommittedStorageBytes);
        pool.RefundUnpublishedValue(tuple);
        Assert.False(pool.IsTracked(tuple));
        Assert.Equal(0, pool.Count);
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        governor.Reserve(tuple.CommittedStorageBytes, null);
        governor.Commit(tuple.CommittedStorageBytes);
        pool.TrackMutable(tuple, tuple.CommittedStorageBytes);
        Assert.True(pool.IsTracked(tuple));
        Assert.Equal(1, pool.Count);
        Assert.Equal(tuple.CommittedStorageBytes + ChargeReclamationPool.EntryChargeBytes + pool.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        GC.KeepAlive(tuple);
    }

    [Fact]
    public void RegistrationDoesNotKeepDroppedTupleAlive()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var weak = MakeDroppedTuple(pool, governor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        Assert.Equal(64 + ChargeReclamationPool.EntryChargeBytes, pool.Sweep(full: true));
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyTuple> MakeDroppedTuple(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var tuple = new PyTuple(new object[] { PyNone.Instance, PyNone.Instance }, governor);
        pool.TrackMutable(tuple, tuple.CommittedStorageBytes);
        return new WeakReference<PyTuple>(tuple);
    }
}
