using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

/// <summary>
/// Debug diagnoses unpaired use; Release caps commits and floors releases.
/// Both configurations must preserve the borrowed root's actual balance.
/// </summary>
public sealed class MemoryGovernorInvariantTests
{
    [Fact]
    public void OverCommitPreservesAdmittedBalance()
    {
        var budget = new LythonMemoryBudget(100);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        governor.Reserve(32, null);
#if DEBUG
        Assert.Throws<InvalidOperationException>(() => governor.Commit(64));
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(32, governor.CurrentReservedBytes);
#else
        governor.Commit(64);
        Assert.Equal(32, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
#endif
        Assert.Equal(32, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void OverReleaseNeverConsumesReservedFunding()
    {
        var budget = new LythonMemoryBudget(100);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        governor.Reserve(48, null);
        governor.Commit(32);
#if DEBUG
        Assert.Throws<InvalidOperationException>(() => governor.Release(64));
        Assert.Equal(32, governor.CurrentCommittedBytes);
        Assert.Equal(48, budget.ReservedBytes);
#else
        governor.Release(64);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(16, budget.ReservedBytes);
#endif
        Assert.Equal(16, governor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void PairedUseStillBalances()
    {
        var governor = new MemoryGovernor(new LythonMemoryBudget(null));
        governor.Reserve(64, null);
        governor.Commit(64);
        Assert.Equal(64, governor.CurrentCommittedBytes);
        governor.Release(64);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
}
