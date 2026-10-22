using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class TeeAdmissionTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void ConstructionAdmitsTheQueueGraphBeforeAllocatingIt()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var context = Context(owner);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var error = Assert.Throws<LythonRuntimeException>(() =>
                new PyTeeSharedState(PyTuple.Empty, 5000, context.MemoryGovernor, context, Span));
#if !DEBUG
            Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 32768);
#endif
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusedFanoutDoesNotAdvanceTheSource(bool asynchronous)
    {
        const int limit = 4096;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = Context(owner);
            var source = new CountingSource();
            var state = new PyTeeSharedState(source, 2, context.MemoryGovernor, context, Span);
            Assert.True(owner.TryReserve(limit - budget.ReservedBytes - 64, out var pin));
            using (pin)
            {
                var baseline = budget.ReservedBytes;
                var error = Assert.Throws<LythonRuntimeException>(() =>
                {
                    if (!asynchronous) state.TryGetNext(0, out _);
                    else
                    {
                        var pending = state.TryGetNextAsync(0);
                        Assert.True(pending.IsCompleted);
                        pending.GetAwaiter().GetResult();
                    }
                });
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Equal(0, source.Moves);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void LockstepConsumptionRetainsCapacityWithoutAccumulatingCharges()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var context = Context(owner);
            var state = new PyTeeSharedState(new CountingSource(), 2, context.MemoryGovernor, context, Span);
            Assert.True(state.TryGetNext(0, out _));
            Assert.True(state.TryGetNext(1, out _));
            var baseline = budget.ReservedBytes;
            for (var i = 0; i < 2000; i++)
            {
                Assert.True(state.TryGetNext(0, out var left));
                Assert.True(state.TryGetNext(1, out var right));
                Assert.Equal(left, right);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingFanoutStaysFundedAndRejectsReentrantSourcePulls(bool cancel)
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            using var cancellation = new CancellationTokenSource();
            var context = Context(owner, cancellation.Token);
            var source = new SuspendedSource(cancellation.Token);
            var state = new PyTeeSharedState(source, 2, context.MemoryGovernor, context, Span);
            var pending = state.TryGetNextAsync(0).AsTask();
            try
            {
                await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(context.MemoryGovernor.CurrentReservedBytes > 0);
                var error = Assert.Throws<LythonRuntimeException>(() => state.TryGetNext(1, out _));
                Assert.Equal("RuntimeError", error.ExceptionType);
                if (cancel) cancellation.Cancel();
                else source.Resume.TrySetResult();
                if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
                else
                {
                    Assert.Equal(BigInteger.One, (await pending.WaitAsync(TimeSpan.FromSeconds(5))).Value);
                    Assert.True(state.TryGetNext(1, out var other));
                    Assert.Equal(BigInteger.One, other);
                }
            }
            finally
            {
                cancellation.Cancel();
                source.Resume.TrySetResult();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static LythonRuntime.ExecutionContext Context(ILythonMemoryBudget owner, CancellationToken token = default)
        => new(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = token });

    private sealed class CountingSource : IPyIterableValue
    {
        public int Moves { get; private set; }
        public IEnumerable<object> Iterate()
        {
            while (true) { Moves++; yield return BigInteger.One; }
        }
    }

    private sealed class SuspendedSource(CancellationToken token) : IPyAsyncIterableValue
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<object> Iterate() => [BigInteger.One];
        public async IAsyncEnumerable<object> IterateAsync()
        {
            Entered.TrySetResult();
            await Resume.Task.WaitAsync(token);
            yield return BigInteger.One;
        }
    }
}
