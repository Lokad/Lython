using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class StringReclamationRegistrationTests
{
    [Fact]
    public void AliasesShareRegistrationWhileEqualStringsRemainDistinct()
    {
        var governor = new MemoryGovernor(null);
        var first = new ChargeReclamationPool(governor);
        var second = new ChargeReclamationPool(governor);
        var text = PyString.FromString("same", governor);
        first.TrackString(text);
        var charged = governor.CurrentCommittedBytes;
        second.TrackString(text);
        Assert.Equal(charged, governor.CurrentCommittedBytes);
        Assert.Equal(1, first.Count);
        Assert.Equal(0, second.Count);

        var equal = PyString.FromString("same", governor);
        second.TrackString(equal);
        Assert.Equal(1, second.Count);
        Assert.NotSame(text.ReclamationEntry, equal.ReclamationEntry);
        first.TrackString(PyString.Empty);
        first.TrackString(PyString.FromString("unowned"));
        Assert.Equal(1, first.Count);
        GC.KeepAlive(text);
        GC.KeepAlive(equal);
    }

    [Fact]
    public void DeniedTierGrowthLeavesRegistrationUnpublishedAndRetryable()
    {
        const long construction = 128 + 4;
        var governor = new MemoryGovernor(construction + ChargeReclamationPool.EntryChargeBytes + 32);
        var pool = new ChargeReclamationPool(governor);
        var text = PyString.FromString("same", governor);
        governor.Reserve(1, null);

        var failure = Assert.Throws<LythonRuntimeException>(() => pool.TrackString(text));
        Assert.Equal("MemoryError", failure.ExceptionType);
        Assert.Null(text.ReclamationEntry);
        Assert.False(pool.IsTracked(text));
        Assert.Equal(0, pool.Count);
        Assert.Equal(1, governor.CurrentReservedBytes);
        Assert.Equal(construction, governor.CurrentCommittedBytes);

        governor.ReleaseReserved(1);
        pool.TrackString(text);
        Assert.True(pool.IsTracked(text));
        Assert.Equal(1, pool.Count);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(governor.MaxAccountedBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(text);
    }

    [Fact]
    public void RefundedUnpublishedStringLeavesNoRegistrationOrValueCharge()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var text = PyString.FromString("éé", governor);
        pool.TrackString(text);
        _ = text.GetRunes();
        pool.RefundUnpublishedValue(text);
        Assert.False(pool.IsTracked(text));
        Assert.Null(text.ReclamationEntry);
        Assert.Equal(0, pool.Count);
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(text);
    }

    [Fact]
    public void RegistrationDoesNotKeepDroppedStringAlive()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var weak = MakeDroppedString(pool, governor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(weak.TryGetTarget(out _));
        Assert.Equal(128 + 4 + ChargeReclamationPool.EntryChargeBytes, pool.Sweep(full: true));
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<PyString> MakeDroppedString(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var text = PyString.FromString("same", governor);
        pool.TrackString(text);
        return new WeakReference<PyString>(text);
    }
}
