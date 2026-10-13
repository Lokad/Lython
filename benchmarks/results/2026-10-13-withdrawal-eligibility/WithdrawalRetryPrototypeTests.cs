using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Xunit.Abstractions;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class WithdrawalRetryPrototypeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedAliasSurvivesActualSlotWithdrawal(bool notifyWithdrawal)
    {
        var governor = new MemoryGovernor(336);
        var pool = new ChargeReclamationPool(governor);
        governor.ReclamationPool = pool;
        var kept = new object();
        governor.Reserve(128, null);
        governor.Commit(128);
        pool.Track(kept, 128);
        governor.Reserve(16, null);
        governor.Commit(16);
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(64, null));
        var before = GC.CollectionCount(GC.MaxGeneration);
        for (var i = 0; i < 8; i++)
        {
            object? slot = kept;
            Withdraw(ref slot, governor, notifyWithdrawal);
            Assert.Null(slot);
            Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(64, null));
        }
        var collections = GC.CollectionCount(GC.MaxGeneration) - before;
        output.WriteLine($"withdrawal-notification={notifyWithdrawal}; pinned-full-collections={collections}; committed={governor.CurrentCommittedBytes}");
        Assert.Equal(notifyWithdrawal ? 8 : 0, collections);
        Assert.Equal(304, governor.CurrentCommittedBytes);
        GC.KeepAlive(kept);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Withdraw(ref object? slot, MemoryGovernor governor, bool notify)
    {
        if (slot is null) return;
        slot = null;
        // Test-only prototype: grant the proposed retry permission using the
        // existing permission setter. This does not reserve/commit/refund bytes.
        if (notify) governor.NotePrefundedAllocation();
    }
}
