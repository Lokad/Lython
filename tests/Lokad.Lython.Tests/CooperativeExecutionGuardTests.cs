using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CooperativeExecutionGuardTests
{
    [Fact]
    public void OrdinaryExecutionHasNoImplicitFuelButRetainsResourceLimits()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
        Assert.Null(context.Limits.MaxExecutionSteps);
        Assert.Equal(LythonRunOptions.DefaultMaxExecutionMemoryBytes, context.Limits.MaxExecutionMemoryBytes);
        Assert.Equal(LythonRunOptions.DefaultMaxRecursionDepth, context.Limits.MaxRecursionDepth);
        Assert.Equal(LythonRunOptions.DefaultMaxHostCalls, context.Limits.MaxHostCalls);
        Assert.Equal(LythonRunOptions.DefaultMaxCollectionSize, context.Limits.MaxCollectionSize);
        Assert.Equal(LythonRunOptions.DefaultMaxStringLength, context.Limits.MaxStringLength);
        Assert.Equal(LythonRunOptions.DefaultMaxHostReadBytes, context.Limits.MaxHostReadBytes);
        Assert.Equal(LythonRunOptions.DefaultMaxStandardOutputBytes, context.Limits.MaxStandardOutputBytes);
        Assert.Equal(LythonRunOptions.DefaultMaxStandardErrorBytes, context.Limits.MaxStandardErrorBytes);
    }

    [Fact]
    public void OrdinaryCheckpointsCanPassTheFormerDefaultAllowance()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
        context.Limits.ExecutionStepCount = LythonRunOptions.DefaultMaxExecutionSteps;
        context.CheckExecution(null);
        // This synthetic boundary supplements the standalone large finite loop;
        // routine unit tests need not execute fifty million checkpoints.
        context.CheckExecution(null);
        Assert.Equal(LythonRunOptions.DefaultMaxExecutionSteps, context.Limits.ExecutionStepCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryCheckpointSweepsBeforeFuelOrCancellationFailure(bool explicitFuel)
    {
        using var cancellation = new CancellationTokenSource();
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 8192,
            MaxExecutionSteps = explicitFuel ? 255 : null,
            CancellationToken = cancellation.Token,
        });
        var dropped = TrackTemporary(state);
        var before = state.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dropped.TryGetTarget(out _));

        for (var i = 0; i < 255; i++) state.Guards.CheckExecution(null);
        Assert.Equal(before, state.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(explicitFuel ? 255 : 0, state.Limits.ExecutionStepCount);
        cancellation.Cancel();
        var span = new LythonSourceSpan(7, 2, 3, 4);
        var failure = Assert.Throws<LythonRuntimeException>(() => state.Guards.CheckExecution(span));

        Assert.Equal(explicitFuel ? "maximum execution step count exceeded (255)" : "execution canceled", failure.Message);
        Assert.Equal(span, failure.Span);
        Assert.Equal(explicitFuel ? 256 : 0, state.Limits.ExecutionStepCount);
        Assert.True(state.MemoryGovernor.CurrentCommittedBytes <= before - 4096);
        Assert.Equal(0, state.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CancellationWithinFuelAllowanceCountsTheCheckpointAndPreservesSpan()
    {
        using var cancellation = new CancellationTokenSource();
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionSteps = 4,
            CancellationToken = cancellation.Token,
        });
        state.Guards.CheckExecution(null);
        cancellation.Cancel();
        var span = new LythonSourceSpan(11, 3, 5, 6);
        var failure = Assert.Throws<LythonRuntimeException>(() => state.Guards.CheckExecution(span));

        Assert.Equal("execution canceled", failure.Message);
        Assert.Equal(span, failure.Span);
        Assert.Equal(2, state.Limits.ExecutionStepCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CheckpointsReclaimDroppedTemporariesWithAndWithoutOptInFuel(bool explicitFuel)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        {
            MaxExecutionMemoryBytes = 8192,
            MaxExecutionSteps = explicitFuel ? 4096 : null,
        });
        var dropped = TrackTemporary(context);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(dropped.TryGetTarget(out _));

        // No guest calls or allocation-pressure sweep supplies reclamation here.
        for (var i = 0; i < 2048; i++) context.CheckExecution(null);

        Assert.True(context.MemoryGovernor.CurrentCommittedBytes <= before - 4096);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        using var funded = context.MemoryGovernor.ReserveTemporary(4096, null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> TrackTemporary(LythonRuntime.ExecutionContext context)
        => TrackTemporary(context.State);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> TrackTemporary(ExecutionState state)
    {
        var value = new object();
        state.MemoryGovernor.Reserve(4096, null);
        state.MemoryGovernor.Commit(4096);
        state.CallTemporaries.Track(value, 4096);
        return new WeakReference<object>(value);
    }
}
