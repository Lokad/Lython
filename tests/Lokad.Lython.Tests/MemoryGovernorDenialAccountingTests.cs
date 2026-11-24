using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// The governor remembers the first denied reservation size for
// failure attribution, so measurements can be reconciled with the budget.
public sealed class MemoryGovernorDenialAccountingTests
{
    [Fact]
    public void DenialIsRecorded()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(100));
        Assert.Equal(0, governor.LastDeniedReservationBytes);
        var first = Assert.Throws<LythonRuntimeException>(() => governor.ReserveTemporary(160, null));
        Assert.Equal(160, governor.LastDeniedReservationBytes);
        Assert.Same(first, Assert.Throws<LythonRuntimeException>(() => governor.ReserveTemporary(200, null)));
        Assert.Equal(160, governor.LastDeniedReservationBytes);
    }

    [Fact]
    public void RefusalPreventsFurtherAdmissionButAllowsRefunds()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(100));
        governor.Reserve(40, null);
        governor.Commit(40);
        var failure = Assert.Throws<LythonRuntimeException>(() => governor.ReserveTemporary(160, null));
        Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, null)));
        Assert.Equal(160, governor.LastDeniedReservationBytes);
        Assert.Equal(40, governor.CurrentCommittedBytes);
        governor.Release(40);
        Assert.Equal(0, governor.CurrentAccountedBytes);
        Assert.Same(failure, Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, null)));
    }

    [Fact]
    public void UnboundedGovernorNeverDenies()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        governor.Reserve(1000000, null);
        governor.Commit(1000000);
        Assert.Equal(0, governor.LastDeniedReservationBytes);
    }

    [Fact]
    public void NestedRefusalRefundsOuterGrowthButKeepsPublishedStorage()
    {
        var budget = new LythonMemoryBudget(100);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var governor = new MemoryGovernor(hook);
        governor.Reserve(24, null);
        governor.Commit(24);
        hook.AfterGrowth = growth =>
        {
            if (growth != 40) return;
            governor.Reserve(8, null);
            governor.Commit(8);
            governor.Reserve(36, null);
        };

        var failure = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(40, null));
        Assert.Same(governor.AdmissionFailure, failure);
        Assert.Equal(36, failure.Attribution?.Requested);
        Assert.Equal(72, failure.Attribution?.Observed);
        Assert.Equal(0, governor.CurrentReservedBytes);
        Assert.Equal(32, governor.CurrentCommittedBytes);
        Assert.Equal(32, budget.ReservedBytes);
        governor.Release(32);
        Assert.Equal(0, budget.ReservedBytes);
    }
}
