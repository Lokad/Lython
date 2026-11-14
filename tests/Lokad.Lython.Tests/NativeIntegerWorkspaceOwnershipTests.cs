using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeIntegerWorkspaceOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(7, 4, 2, 3);

    [Fact]
    public void EndingReuseKeepsOutstandingWordsFundedUntilTheirUserFinishes()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
        var governor = context.MemoryGovernor;
        var baseline = budget.ReservedBytes;
        var active = governor.RentNativeIntegerWorkspace(32, Span, context);
        active[0] = 42;
        var charge = budget.ReservedBytes - baseline;
        var idle = governor.RentNativeIntegerWorkspace(32, Span, context);
        governor.ReturnNativeIntegerWorkspace(idle);
        governor.EndWorkspaceReuse();
        Assert.Equal(baseline + charge, budget.ReservedBytes);
        Assert.Equal(42, active[0]);
        governor.ReturnNativeIntegerWorkspace(active);
        Assert.Equal(baseline, budget.ReservedBytes);
        governor.EndWorkspaceReuse();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public void IdleCapacityRemainsChargedThroughSmallerBorrowAndEndsWithExecutionReuse()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
        var governor = context.MemoryGovernor;
        var baseline = budget.ReservedBytes;
        var first = governor.RentNativeIntegerWorkspace(32, Span, context);
        var charge = budget.ReservedBytes;
        governor.ReturnNativeIntegerWorkspace(first);
        Assert.Equal(charge, budget.ReservedBytes);
        var second = governor.RentNativeIntegerWorkspace(24, Span, context);
        Assert.Equal(charge, budget.ReservedBytes);
        governor.ReturnNativeIntegerWorkspace(second);
        governor.EndWorkspaceReuse();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedLoanPreservesActiveWordsAndTheFirstOrigin()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
        var governor = context.MemoryGovernor;
        var baseline = budget.ReservedBytes;
        var active = governor.RentNativeIntegerWorkspace(32, Span, context);
        active[0] = 37;
        var charge = budget.ReservedBytes;
        hook.RefuseGrowth = _ => true;
        var first = Assert.Throws<LythonRuntimeException>(() => governor.RentNativeIntegerWorkspace(64, Span, context));
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, first.Attribution?.Reason);
        Assert.Same(Span, first.Span);
        Assert.Same(first, Assert.Throws<LythonRuntimeException>(() => governor.RentNativeIntegerWorkspace(32, Span, context)));
        Assert.Equal(charge, budget.ReservedBytes);
        Assert.Equal(37, active[0]);
        governor.ReturnNativeIntegerWorkspace(active);
        governor.EndWorkspaceReuse();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public void CancellationDuringAdmissionUnwindsTheUnallocatedLoan()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
        var baseline = budget.ReservedBytes;
        hook.AfterGrowth = _ => cancellation.Cancel();
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            context.MemoryGovernor.RentNativeIntegerWorkspace(32, Span, context));
        Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        Assert.Same(Span, failure.Span);
        Assert.Equal(baseline, budget.ReservedBytes);
        context.MemoryGovernor.EndWorkspaceReuse();
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
