using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class BatchedSharedAdmissionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SharedRefusalPrecedesTheUnfundedSourcePull(bool asynchronous, bool growth)
    {
        const long capacity = 65536;
        var budget = new LythonMemoryBudget(capacity);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var source = new Source(8);
        var batch = new PyBatchedIterator(source, 8, false, context.MemoryGovernor, new(1, 1, 1, 1), context);
        var held = budget.ReservedBytes;
        var room = growth ? PyTuple.EstimateApproximateBytes(4) : 0;
        Assert.True(budget.TryReserve(capacity - held - room, out var blocker));
        using (blocker)
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await batch.TryMoveNextAsync();
                else batch.TryMoveNext(out _);
            });
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(growth ? 4 : 0, source.Pulls);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(capacity - room, budget.ReservedBytes);
        }
        Assert.Equal(held, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("fault")]
    [InlineData("cancel")]
    public async Task PartialBatchStaysFundedUntilTheSuspendedUserFinishes(string route)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var source = new Source(2) { PauseSecond = true, FailSecond = route == "fault" };
        var batch = new PyBatchedIterator(source, 2, false, context.MemoryGovernor, new(1, 1, 1, 1), context);
        var baseline = budget.ReservedBytes;
        var pending = batch.TryMoveNextAsync().AsTask();
        try
        {
            await source.Paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline, "The live partial batch needs shared admission while its source is suspended.");
        }
        finally
        {
            if (route == "cancel") cancellation.Cancel();
            source.Resume.TrySetResult();
            if (route == "success")
            {
                var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(2, Assert.IsType<PyTuple>(result.Value).Count);
            }
            else
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                if (route == "cancel") Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
                else Assert.Equal("ValueError", failure.ExceptionType);
            }
        }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        if (route == "success") Assert.True(budget.ReservedBytes > baseline); // The tuple retains funding.
        else Assert.Equal(baseline, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShortBatchCopyRefusalReleasesThePrivateBuffer(bool asynchronous)
    {
        const long capacity = 65536;
        var budget = new LythonMemoryBudget(capacity);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var source = new Source(3);
        var batch = new PyBatchedIterator(source, 8, false, context.MemoryGovernor, new(1, 1, 1, 1), context);
        var held = budget.ReservedBytes;
        var room = PyTuple.EstimateApproximateBytes(4);
        Assert.True(budget.TryReserve(capacity - held - room, out var blocker));
        using (blocker)
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await batch.TryMoveNextAsync();
                else batch.TryMoveNext(out _);
            });
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Equal(4, source.Pulls); // Three values and the exhaustion pull.
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(capacity - room, budget.ReservedBytes);
        }
        Assert.Equal(held, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Source(int count) : PyIteratorBase
    {
        internal int Pulls;
        internal bool PauseSecond;
        internal bool FailSecond;
        internal readonly TaskCompletionSource Paused = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override bool TryMoveNext(out object value)
        {
            Pulls++;
            if (FailSecond && Pulls == 2) throw new LythonRuntimeException("ValueError", "source failed", null);
            value = PyNone.Instance;
            return Pulls <= count;
        }
        public override async ValueTask<PyIterationResult> TryMoveNextAsync()
        {
            if (PauseSecond && Pulls == 1)
            {
                Paused.TrySetResult();
                await Resume.Task;
            }
            return TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End;
        }
        public override PyString RenderPython(PyRenderingContext context) => PyString.FromString("<source>");
    }
}
