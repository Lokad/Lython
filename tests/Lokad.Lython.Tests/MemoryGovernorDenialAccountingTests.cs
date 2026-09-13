using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

// MG24: the governor remembers the most recent denied reservation size for
// failure attribution, so measurements can be reconciled with the budget.
public sealed class MemoryGovernorDenialAccountingTests
{
    [Fact]
    public void DenialIsRecorded()
    {
        var governor = new MemoryGovernor(100);
        Assert.Equal(0, governor.LastDeniedReservationBytes);
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(160, null));
        Assert.Equal(160, governor.LastDeniedReservationBytes);
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(200, null));
        Assert.Equal(200, governor.LastDeniedReservationBytes);
    }

    [Fact]
    public void SuccessPreservesTheRecord()
    {
        var governor = new MemoryGovernor(100);
        Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(160, null));
        governor.Reserve(40, null);
        governor.Commit(40);
        Assert.Equal(160, governor.LastDeniedReservationBytes);
        Assert.Equal(40, governor.CurrentCommittedBytes);
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
