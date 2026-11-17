using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CallablePacketOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefusedWideCallStoragePreventsCallableEffects(bool bound)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var callable = new PendingCallable();
            object target = bound ? new PyBoundMethod(PyNone.Instance, callable) : callable;
            var arguments = Enumerable.Repeat(CallArgumentValue.Positional(PyNone.Instance), 8).ToArray();
            var baseline = budget.ReservedBytes;
            hook.RefuseGrowth = _ => true;
            var failure = Assert.Throws<LythonRuntimeException>(() =>
                CallableInvocation.Invoke(target, arguments, Span, context));
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(0, callable.Calls);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            context.State.CloseResources();
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationKeepsWideCallFundedUntilCleanupFinishes(bool bound)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var value = new object();
            var callable = new PendingCallable();
            object target = bound ? new PyBoundMethod(value, callable) : callable;
            var arguments = Enumerable.Repeat(CallArgumentValue.Positional(value), 8).ToArray();
            var baseline = budget.ReservedBytes;
            var pending = CallableInvocation.InvokeAsync(target, arguments, Span, context).AsTask();
            try
            {
                await callable.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
                await callable.CleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                callable.ReleaseCleanup.TrySetResult();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                Assert.True(callable.ValuesSurvivedCleanup);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                cancellation.Cancel();
                callable.ReleaseCleanup.TrySetResult();
                try { await pending; } catch (OperationCanceledException) { }
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(budget.LimitBytes!.Value, out var reused));
        reused.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class PendingCallable : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool ValuesSurvivedCleanup;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource CleanupEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource ReleaseCleanup = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return PyNone.Instance;
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            LythonRuntime.ExecutionContext context)
        {
            Calls++;
            var values = arguments.Select(value => value.Value).ToArray();
            Entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, context.State.CancellationToken); }
            finally
            {
                CleanupEntered.TrySetResult();
                await ReleaseCleanup.Task;
                ValuesSurvivedCleanup = arguments.Select(value => value.Value).SequenceEqual(values);
            }
            return PyNone.Instance;
        }
    }
}

