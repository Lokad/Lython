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
    {
        var value = new object();
        context.MemoryGovernor.Reserve(4096, null);
        context.MemoryGovernor.Commit(4096);
        context.State.CallTemporaries.Track(value, 4096);
        return new WeakReference<object>(value);
    }
}
