using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CooperativeExecutionGuardTests
{
    [Fact]
    public void OrdinaryExecutionRetainsResourceLimits()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
        Assert.Null(Assert.IsType<LythonMemoryBudget>(context.MemoryGovernor.Budget).LimitBytes);
        Assert.Equal(LythonRunOptions.DefaultMaxRecursionDepth, context.Limits.MaxRecursionDepth);
    }

    [Fact]
    public void CancellationPreservesOwnershipAndSourceSpan()
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(8192);
        using var owner = budget.CreateScope();
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = owner,
            CancellationToken = cancellation.Token,
        });
        var dropped = CreateWorkingStorage(state);
        var before = state.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal(before, budget.ReservedBytes);

        for (var i = 0; i < 255; i++) state.Guards.CheckExecution(null);
        Assert.Equal(before, state.MemoryGovernor.CurrentCommittedBytes);
        cancellation.Cancel();
        var span = new LythonSourceSpan(7, 2, 3, 4);
        var failure = Assert.Throws<LythonRuntimeException>(() => state.Guards.CheckExecution(span));

        Assert.Equal("execution canceled", failure.Message);
        Assert.Equal(span, failure.Span);
        Assert.Equal(before, state.MemoryGovernor.CurrentCommittedBytes);
        GC.KeepAlive(dropped);
        dropped = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        Assert.Equal(0, state.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CancellationPreservesSourceSpan()
    {
        using var cancellation = new CancellationTokenSource();
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            CancellationToken = cancellation.Token,
        });
        state.Guards.CheckExecution(null);
        cancellation.Cancel();
        var span = new LythonSourceSpan(11, 3, 5, 6);
        var failure = Assert.Throws<LythonRuntimeException>(() => state.Guards.CheckExecution(span));

        Assert.Equal("execution canceled", failure.Message);
        Assert.Equal(span, failure.Span);
    }

    [Fact]
    public void CancellationChecksPreserveBatchOwnership()
    {
        var budget = new LythonMemoryBudget(8192);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = owner,
        });
        var dropped = CreateWorkingStorage(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal(before, budget.ReservedBytes);

        // Checkpoints neither sweep nor retire a still-active owner.
        for (var i = 0; i < 2048; i++) context.CheckExecution(null);

        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(dropped);
        dropped = null!;
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        using var next = budget.CreateScope();
        Assert.True(next.TryReserve(4096, out _));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object CreateWorkingStorage(LythonRuntime.ExecutionContext context)
        => CreateWorkingStorage(context.State);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object CreateWorkingStorage(ExecutionState state)
    {
        state.MemoryGovernor.Reserve(4096, null);
        var value = new byte[4096];
        state.MemoryGovernor.Commit(4096);
        return value;
    }
}
