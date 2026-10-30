using Lokad.Lython.Tests.Harness;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class SynchronousHostCompletionTests
{
    [Fact]
    public async Task LateSubprocessCompletionIsDisposedBeforeRejectingTheSynchronousPromise()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var resultOwner = owner.CreateScope();
        Assert.True(resultOwner.TryReserve(256, out _));
        using var result = new LythonSubprocessResult(0, "result"u8.ToArray(), ReadOnlyMemory<byte>.Empty, resultOwner);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<LythonSubprocessResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new ObservedValueTask<LythonSubprocessResult>(completion.Task, entered);
        var run = Task.Run(() => Record.Exception(() => HostOperation.Await(new Capability(true),
            () => observed.Value, "subprocess.run", null, new MemoryGovernor(owner))));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(run.IsCompleted);
            Assert.Equal(256, budget.ReservedBytes);
            completion.TrySetResult(result);
            var error = Assert.IsAssignableFrom<LythonRuntimeException>(await run);
            Assert.Equal(LythonFailureReason.HostOperationFailed, error.Attribution?.Reason);
            Assert.Equal(0, budget.ReservedBytes);
            Assert.Throws<ObjectDisposedException>(() => result.StandardOutputUtf8);
        }
        finally { completion.TrySetResult(result); await run; }
    }

    [Theory]
    [InlineData(false, "success"), InlineData(true, "success")]
    [InlineData(false, "fault"), InlineData(true, "fault")]
    [InlineData(false, "cancel"), InlineData(true, "cancel")]
    public async Task PendingResultsSettleBeforeTheContractFailure(bool generic, string outcome)
    {
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cause = new IOException("host completion");
        var observed = new ObservedValueTask<int>(completion.Task, entered);
        var run = Task.Run(() => Record.Exception(() =>
        {
            if (generic) _ = HostOperation.Await(new Capability(true), () => observed.Value,
                "read", null, new MemoryGovernor(null));
            else HostOperation.Await(new Capability(true), () => observed.Untyped,
                "write", null, new MemoryGovernor(null));
        }));
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            if (outcome == "fault") completion.TrySetException(cause);
            else if (outcome == "cancel") completion.TrySetCanceled();
            else completion.TrySetResult(42);
            var failure = Assert.IsAssignableFrom<LythonRuntimeException>(await run);
            Assert.Equal(LythonFailureReason.HostOperationFailed, failure.Attribution?.Reason);
            Assert.Contains("violated its synchronous host capability contract", failure.Message);
            if (outcome == "fault") Assert.Same(cause, failure.InnerException);
            if (outcome == "cancel") Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);
        }
        finally { completion.TrySetResult(42); await run; }
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public void CompletionDuringInspectionStillRetiresThePendingPromise(bool generic)
    {
        var completion = new TaskCompletionSource<int>();
        var observedPending = new TaskCompletionSource();
        var observed = new ObservedValueTask<int>(completion.Task, observedPending,
            () => completion.TrySetResult(42));
        var error = Assert.ThrowsAny<LythonRuntimeException>(() =>
        {
            if (generic) _ = HostOperation.Await(new Capability(true), () => observed.Value,
                "read", null, new MemoryGovernor(null));
            else HostOperation.Await(new Capability(true), () => observed.Untyped,
                "write", null, new MemoryGovernor(null));
        });
        Assert.True(observedPending.Task.IsCompletedSuccessfully);
        Assert.True(completion.Task.IsCompletedSuccessfully);
        Assert.Equal(LythonFailureReason.HostOperationFailed, error.Attribution?.Reason);
        Assert.Contains("violated its synchronous host capability contract", error.Message);
    }

    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public void AlreadyCompletedFailuresDoNotBecomePendingPromises(bool generic, bool cancelled)
    {
        var cause = new IOException("private completed failure");
        var task = cancelled ? Task.FromCanceled<int>(new CancellationToken(true)) : Task.FromException<int>(cause);
        var error = Assert.ThrowsAny<LythonRuntimeException>(() =>
        {
            if (generic) _ = HostOperation.Await(new Capability(true), () => new ValueTask<int>(task),
                "read", null, new MemoryGovernor(null));
            else HostOperation.Await(new Capability(true), () => new ValueTask(task),
                "write", null, new MemoryGovernor(null));
        });
        Assert.Equal(cancelled ? LythonFailureReason.Cancelled : LythonFailureReason.HostOperationFailed,
            error.Attribution?.Reason);
        Assert.DoesNotContain("violated its synchronous host capability contract", error.Message);
        if (!cancelled) Assert.Same(cause, error.InnerException);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public void AsyncOnlyCapabilitiesRejectBeforeStartingWork(bool generic)
    {
        var invoked = false;
        var error = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (generic) _ = HostOperation.Await(new Capability(false), () =>
            {
                invoked = true; return ValueTask.FromResult(42);
            }, "read", null, new MemoryGovernor(null));
            else HostOperation.Await(new Capability(false), () =>
            {
                invoked = true; return ValueTask.CompletedTask;
            }, "write", null, new MemoryGovernor(null));
        });
        Assert.False(invoked);
        Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, error.Attribution?.Reason);
    }

    private sealed record Capability(bool CompletesSynchronously) : ILythonSynchronousHostCapability;
}
