using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CallablePacketOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RefusedCallStoragePreventsCallableEffects(bool bound, bool starmap)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var callable = new PendingCallable();
            object target = bound ? new PyBoundMethod(PyNone.Instance, callable) : callable;
            var arguments = Enumerable.Repeat(CallArgumentValue.Positional(PyNone.Instance), 8).ToArray();
            var iterator = starmap ? CreateStarmap(target, new PyList(Enumerable.Repeat<object>(PyNone.Instance, 8)), context) : null;
            var baseline = budget.ReservedBytes;
            hook.RefuseGrowth = _ => true;
            var failure = Assert.Throws<LythonRuntimeException>(() =>
            {
                if (iterator is not null) _ = iterator.TryMoveNext();
                else _ = CallableInvocation.Invoke(target, arguments, Span, context);
            });
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CancellationKeepsWideCallFundedUntilCleanupFinishes(bool bound, bool starmap)
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
            Task pending = starmap
                ? CreateStarmap(target, new PyList(Enumerable.Repeat(value, 8)), context).TryMoveNextAsync().AsTask()
                : CallableInvocation.InvokeAsync(target, arguments, Span, context).AsTask();
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
                if (bound)
                {
                    Assert.True(budget.ReservedBytes > baseline);
                    Assert.All(callable.RetainedArguments!, argument => Assert.Same(value, argument.Value));
                }
                else
                {
                    Assert.Equal(baseline, budget.ReservedBytes);
                    Assert.All(callable.RetainedArguments!, argument => Assert.Null(argument.Value));
                }
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundCopiesRemainUnchangedAndMajorCapacityStaysFunded(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var receiver = new object();
                var callable = new RetainingCallable();
                object target = new PyBoundMethod(receiver, callable);
                foreach (var width in new[] { 1, 8 })
                {
                    var value = new object();
                    var arguments = Enumerable.Repeat(CallArgumentValue.Positional(value), width).ToArray();
                    arguments[^1] = CallArgumentValue.Keyword("last", value);
                    var baseline = budget.ReservedBytes;
                    var first = Assert.IsType<CallArgumentValue[]>(asynchronous
                        ? await CallableInvocation.InvokeAsync(target, arguments, Span, context)
                        : CallableInvocation.Invoke(target, arguments, Span, context));
                    Assert.NotSame(arguments, first);
                    if (width > 3) Assert.True(budget.ReservedBytes > baseline);
                    Assert.Equal(arguments, first.Skip(1));
                    Assert.Same(receiver, first[0].Value);
                    var saved = first.ToArray();
                    Array.Clear(arguments);
                    var next = asynchronous
                        ? await CallableInvocation.InvokeAsync(target, [CallArgumentValue.Positional(PyNone.Instance)], Span, context)
                        : CallableInvocation.Invoke(target, [CallArgumentValue.Positional(PyNone.Instance)], Span, context);
                    Assert.NotSame(first, next);
                    Assert.Equal(saved, first);
                    if (width > 3) Assert.True(budget.ReservedBytes > baseline);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                }
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.True(budget.TryReserve(budget.LimitBytes!.Value, out var reused));
        reused.Dispose();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StarmapReadsTheLiveListAfterAdmissionChangesItsLength(bool asynchronous, bool grow)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
        try
        {
            object[] original = [new(), new(), new(), new(), new()];
            var row = new PyList(original);
            var appended = new object();
            var callable = new PositionalCount();
            var iterator = CreateStarmap(callable, row, context);
            var baseline = budget.ReservedBytes;
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                if (grow) row.Add(appended);
                else row.RemoveAt(4);
            };

            var result = asynchronous ? await iterator.TryMoveNextAsync() : iterator.TryMoveNext();

            Assert.True(result.HasValue);
            Assert.Equal(grow ? 6 : 4, result.Value);
            Assert.Equal(grow ? [.. original, appended] : original[..4], callable.Values);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        finally { context.State.CloseResources(); }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static PyStarmapIterator CreateStarmap(object target, PyList row, LythonRuntime.ExecutionContext context)
        => new(target, PyIteration.Cursor.Create(new PyList(new object[] { row }), Span, context), context, Span, borrowStorage: false);

    private sealed class PositionalCount : LythonRuntime.ICallable
    {
        internal object[] Values { get; private set; } = [];

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Values = arguments.Select(argument => argument.Value).ToArray();
            return arguments.Length;
        }
    }

    private sealed class RetainingCallable : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => arguments;

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span,
            LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return arguments;
        }
    }

    private sealed class PendingCallable : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool ValuesSurvivedCleanup;
        internal CallArgumentValue[]? RetainedArguments;
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
            RetainedArguments = arguments;
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

