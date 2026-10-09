using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class MemoryGovernorReclaimProgressTests
{
    [Theory]
    [InlineData(16, 16)]
    [InlineData(64, 16)]
    public void NewCommitAllowsReliefEvenWhenNetCommittedBytesDoNotGrow(int previousCharge, int replacementCharge)
    {
        var governor = new MemoryGovernor(320 + previousCharge);
        var pool = new ChargeReclamationPool(governor);
        governor.LivePoolProvider = () => [pool];
        var target = DenyWhileRetainedThenReplaceAndDrop(governor, pool, previousCharge, replacementCharge);

        // The replacement has made allocation progress, but the committed
        // total is equal to or below its level at the previous denial.
        Assert.Equal(320 + replacementCharge, governor.CurrentCommittedBytes);
        governor.EnsureCanReserve(64, null);

        Assert.False(target.TryGetTarget(out _));
        Assert.Equal(64 + replacementCharge, governor.CurrentCommittedBytes);
        Assert.Equal(0, pool.Count);
    }

    [Fact]
    public void RepeatedPinnedDenialWithoutNewCommitDoesNotRepeatRelief()
    {
        var governor = new MemoryGovernor(336);
        var pool = new ChargeReclamationPool(governor);
        var enumerations = 0;
        governor.LivePoolProvider = () => { enumerations++; return [pool]; };
        var value = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        pool.Track(value, 128);
        governor.Reserve(16, null);
        governor.Commit(16);

        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(64, null));
        Assert.True(enumerations > 0);
        var afterFirstRelief = enumerations;
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(64, null));
        Assert.Equal(afterFirstRelief, enumerations);
        Assert.Equal(336, governor.CurrentCommittedBytes);
        GC.KeepAlive(value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> DenyWhileRetainedThenReplaceAndDrop(
        MemoryGovernor governor, ChargeReclamationPool pool, int previousCharge, int replacementCharge)
    {
        var value = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        pool.Track(value, 128);
        governor.Reserve(previousCharge, null);
        governor.Commit(previousCharge);

        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(64, null));
        governor.Release(previousCharge);
        governor.Reserve(replacementCharge, null);
        governor.Commit(replacementCharge);
        var target = new WeakReference<object>(value);
        GC.KeepAlive(value);
        return target;
    }
}
