using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class SynchronousHostCompletionTests
{
    [Theory]
    [InlineData(false, "success"), InlineData(true, "success")]
    [InlineData(false, "fault"), InlineData(true, "fault")]
    [InlineData(false, "cancel"), InlineData(true, "cancel")]
    public async Task PendingResultsSettleBeforeTheContractFailure(bool generic, string outcome)
    {
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cause = new IOException("host completion");
        var run = Task.Run(() => Record.Exception(() =>
        {
            if (generic) _ = HostOperation.Await(new Capability(true), () =>
            {
                entered.TrySetResult(); return new ValueTask<int>(completion.Task);
            }, "read", null);
            else HostOperation.Await(new Capability(true), () =>
            {
                entered.TrySetResult(); return new ValueTask(completion.Task);
            }, "write", null);
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
    public void AsyncOnlyCapabilitiesRejectBeforeStartingWork(bool generic)
    {
        var invoked = false;
        var error = Assert.Throws<LythonRuntimeException>(() =>
        {
            if (generic) _ = HostOperation.Await(new Capability(false), () =>
            {
                invoked = true; return ValueTask.FromResult(42);
            }, "read", null);
            else HostOperation.Await(new Capability(false), () =>
            {
                invoked = true; return ValueTask.CompletedTask;
            }, "write", null);
        });
        Assert.False(invoked);
        Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, error.Attribution?.Reason);
    }

    private sealed record Capability(bool CompletesSynchronously) : ILythonSynchronousHostCapability;
}
