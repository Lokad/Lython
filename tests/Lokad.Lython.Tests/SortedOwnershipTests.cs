using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class SortedOwnershipTests
{
    private static readonly Func<object[], LythonSourceSpan, Context, object> Sort = typeof(LythonRuntime)
        .GetMethod("Sorted", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, object>>();
    private static readonly Func<object[], LythonSourceSpan, Context, ValueTask<object>> SortAsync = typeof(LythonRuntime)
        .GetMethod("SortedAsync", BindingFlags.Static | BindingFlags.NonPublic)!
        .CreateDelegate<Func<object[], LythonSourceSpan, Context, ValueTask<object>>>();

    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "failure")]
    [InlineData(true, "failure")]
    [InlineData(false, "cancel")]
    [InlineData(true, "cancel")]
    [InlineData(false, "refusal")]
    [InlineData(true, "refusal")]
    public async Task MaterializedInputStaysFundedThroughKeysAndUnwind(bool asynchronous, string outcome)
    {
        var budget = new LythonMemoryBudget(65536);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
        object shared = BigInteger.One;
        var source = Enumerable.Repeat(shared, 128).ToArray();
        var key = new Key(() =>
        {
            Assert.True(context.MemoryGovernor.CurrentReservedBytes >= 32 + 16 * source.Length);
            if (outcome == "failure") throw new LythonRuntimeException("ValueError", "key failed", null);
            if (outcome == "cancel") { cancellation.Cancel(); context.CheckExecution(null); }
            if (outcome == "refusal") context.MemoryGovernor.Reserve(65536, null);
        }, asynchronous);
        Task<object> execution;
        if (asynchronous) execution = SortAsync([source, key], new(0, 0, 0, 0), context).AsTask();
        else
        {
            try { execution = Task.FromResult(Sort([source, key], new(0, 0, 0, 0), context)); }
            catch (LythonRuntimeException error) { execution = Task.FromException<object>(error); }
        }
        try
        {
            if (asynchronous)
            {
                await key.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(execution.IsCompleted);
                Assert.True(budget.ReservedBytes >= 32 + 16 * source.Length);
                key.Resume.TrySetResult();
            }
            if (outcome == "success")
            {
                var result = Assert.IsType<PyList>(await execution);
                Assert.Equal(source.Length, result.Count);
                foreach (var item in result) Assert.Same(shared, item);
                Assert.True(context.MemoryGovernor.CurrentCommittedBytes > 0);
            }
            else
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => execution);
                if (outcome == "refusal") Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(1, key.Calls);
                Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
            }
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        finally
        {
            key.Resume.TrySetResult();
            try { await execution; } catch (LythonRuntimeException) { }
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Key(Action inspect, bool asynchronous) : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            if (++Calls == 1) inspect();
            return BigInteger.Zero;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            if (asynchronous && Calls == 0)
            {
                Entered.TrySetResult();
                await Resume.Task;
            }
            return Invoke(arguments, span, context);
        }
    }
}
