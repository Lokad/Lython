using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class FunctionWorkspaceOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(7, 4, 2, 3);

    [Fact]
    public void SequentialWorkspaceReuseRemainsFundedAndClearedUntilOwnershipEnds()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var first = governor.RentFunctionWorkspace(12, Span);
            var charge = budget.ReservedBytes;
            Assert.True(charge > 0);
            first[11] = new object();
            governor.ReturnFunctionWorkspace(first, charge);
            Assert.Equal(charge, budget.ReservedBytes);
            Assert.All(first, value => Assert.Null(value));
            var second = governor.RentFunctionWorkspace(12, Span);
            Assert.Same(first, second);
            Assert.Equal(charge, budget.ReservedBytes);
            governor.ReturnFunctionWorkspace(second, charge);
            governor.EndFunctionWorkspaceReuse();
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ExecutionsSharingAllowanceKeepDistinctWorkspacesAndReturnTheirOwnFunding()
    {
        var budget = new LythonMemoryBudget(4096);
        using var firstOwner = budget.CreateScope();
        using var secondOwner = budget.CreateScope();
        var first = new MemoryGovernor(firstOwner);
        var second = new MemoryGovernor(secondOwner);
        var a = first.RentFunctionWorkspace(12, Span);
        var charge = budget.ReservedBytes;
        first.ReturnFunctionWorkspace(a, charge);
        var b = second.RentFunctionWorkspace(12, Span);
        Assert.NotSame(a, b);
        Assert.Equal(2 * charge, budget.ReservedBytes);
        second.ReturnFunctionWorkspace(b, charge);
        first.EndFunctionWorkspaceReuse();
        Assert.Equal(charge, budget.ReservedBytes);
        second.EndFunctionWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ReuseEndingReleasesOnlyIdleStorageAndLateReturnsCannotRecache()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var active = governor.RentFunctionWorkspace(12, Span);
        var charge = budget.ReservedBytes;
        active[0] = new object();
        var idle = governor.RentFunctionWorkspace(12, Span);
        Assert.NotSame(active, idle);
        governor.ReturnFunctionWorkspace(idle, charge);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(charge, budget.ReservedBytes);
        Assert.NotNull(active[0]);
        governor.ReturnFunctionWorkspace(active, charge);
        Assert.All(active, value => Assert.Null(value));
        Assert.Equal(0, budget.ReservedBytes);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void MismatchedIdleCapacityEndsBeforeOneNewAdmission()
    {
        var budget = new LythonMemoryBudget(256);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var first = governor.RentFunctionWorkspace(12, Span);
        var firstCharge = budget.ReservedBytes;
        governor.ReturnFunctionWorkspace(first, firstCharge);
        var second = governor.RentFunctionWorkspace(8, Span);
        Assert.NotSame(first, second);
        Assert.True(budget.ReservedBytes < firstCharge);
        Assert.Equal(MemoryGovernor.FunctionWorkspaceBytes(8), budget.ReservedBytes);
        governor.ReturnFunctionWorkspace(second, budget.ReservedBytes);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void FirstRefusalCannotRecoverThroughCachedCapacityAndCleanupStillRefundsIt()
    {
        var budget = new LythonMemoryBudget(256);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var cached = governor.RentFunctionWorkspace(12, Span);
        governor.ReturnFunctionWorkspace(cached, budget.ReservedBytes);
        var first = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, Span));
        var again = Assert.Throws<LythonRuntimeException>(() => governor.RentFunctionWorkspace(12, Span));
        Assert.Same(first, again);
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, first.Attribution?.Reason);
        Assert.Equal(1, first.Attribution?.Requested);
        Assert.Equal(Span, first.Span);
        Assert.Equal(256, budget.ReservedBytes);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
