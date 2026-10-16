using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

// The governor remembers the first denied reservation size for
// failure attribution, so measurements can be reconciled with the budget.
public sealed class MemoryGovernorDenialAccountingTests
{
    [Fact]
    public void DenialIsRecorded()
    {
        var governor = new MemoryGovernor(100);
        Assert.Equal(0, governor.LastDeniedReservationBytes);
        var first = Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(160, null));
        Assert.Equal(160, governor.LastDeniedReservationBytes);
        Assert.Same(first, Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(200, null)));
        Assert.Equal(160, governor.LastDeniedReservationBytes);
    }

    [Fact]
    public void RefusalPreventsFurtherAdmissionButAllowsRefunds()
    {
        var governor = new MemoryGovernor(100);
        governor.Reserve(40, null);
        governor.Commit(40);
        var failure = Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(160, null));
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
        var governor = new MemoryGovernor(null);
        governor.Reserve(1000000, null);
        governor.Commit(1000000);
        Assert.Equal(0, governor.LastDeniedReservationBytes);
    }
}
