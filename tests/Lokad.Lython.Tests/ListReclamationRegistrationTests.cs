using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class ListReclamationRegistrationTests
{
    [Fact]
    public void AliasesShareRegistrationAndCopiedListsOwnTheirStorage()
    {
        var governor = new MemoryGovernor(null);
        var first = new ChargeReclamationPool(governor);
        var second = new ChargeReclamationPool(governor);
        var original = new PyList(Enumerable.Repeat<object>(PyNone.Instance, 17), governor);
        first.TrackMutable(original, original.CommittedStorageBytes);
        var charged = governor.CurrentCommittedBytes;
        second.TrackMutable(original, original.CommittedStorageBytes);
        Assert.Equal(charged, governor.CurrentCommittedBytes);
        Assert.Equal(1, first.Count);
        Assert.Equal(0, second.Count);

        var copy = new PyList(original);
        Assert.False(second.IsTracked(copy));
        second.TrackMutable(copy, copy.CommittedStorageBytes);
        original.Clear();
        Assert.Empty(original);
        Assert.Equal(17, copy.Count);
        Assert.Equal(original.CommittedStorageBytes + copy.CommittedStorageBytes +
            2 * ChargeReclamationPool.EntryChargeBytes + first.CommittedBackingBytes + second.CommittedBackingBytes,
            governor.CurrentCommittedBytes);
        first.Sweep(full: true);
        second.Sweep(full: true);
        Assert.Equal(1, first.Count);
        Assert.Equal(1, second.Count);
        GC.KeepAlive(original);
        GC.KeepAlive(copy);
    }

    [Fact]
    public void DeniedListRegistrationLeavesTheRetainedValueRetryable()
    {
        const long construction = 192;
        var governor = new MemoryGovernor(construction + ChargeReclamationPool.EntryChargeBytes + 32);
        var pool = new ChargeReclamationPool(governor);
        var list = new PyList([], governor);
        governor.Reserve(1, null);
        var failure = Assert.Throws<LythonRuntimeException>(() => pool.TrackMutable(list, list.CommittedStorageBytes));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.False(pool.IsTracked(list));
        Assert.Equal(0, pool.Count);
        Assert.Equal(1, governor.CurrentReservedBytes);
        Assert.Equal(construction, governor.CurrentCommittedBytes);

        governor.ReleaseReserved(1);
        pool.TrackMutable(list, list.CommittedStorageBytes);
        Assert.True(pool.IsTracked(list));
        Assert.Equal(1, pool.Count);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(governor.MaxAccountedBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(list);
    }

    [Fact]
    public void RefundedUnpublishedListLeavesNoRegistrationOrStorageCharge()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var list = new PyList([], governor);
        pool.TrackMutable(list, list.CommittedStorageBytes);
        list.AddRange(Enumerable.Repeat<object>(PyNone.Instance, 17));
        pool.RefundUnpublishedValue(list);
        Assert.False(pool.IsTracked(list));
        Assert.Equal(0, pool.Count);
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(list);
    }

    [Fact]
    public void RegistrationDoesNotKeepDroppedListAlive()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var weak = MakeDroppedList(pool, governor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        Assert.Equal(192 + ChargeReclamationPool.EntryChargeBytes, pool.Sweep(full: true));
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyList> MakeDroppedList(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var list = new PyList([], governor);
        pool.TrackMutable(list, list.CommittedStorageBytes);
        return new WeakReference<PyList>(list);
    }
}
