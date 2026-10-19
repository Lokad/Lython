using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class LenProtocolAsyncTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("admission")]
    public async Task LengthBodySuspendsOnceAndKeepsOwnershipUntilCompletion(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var callback = new LengthCallback();
        context.Variables["getsize"] = callback;
        var source = "class V:\n def __len__(self):return getsize()\nreturn len(V())";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        var origin = new LythonSourceSpan(1000, 5, 50, 1);
        try
        {
            var first = await Task.WhenAny(callback.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(callback.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
            {
                var governor = context.MemoryGovernor;
                _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(
                    budget.LimitBytes!.Value - governor.CurrentAccountedBytes + 1, origin));
            }
            Assert.False(callback.Finished);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            callback.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) when (route != "success") { }
        }
        if (route == "success") Assert.Equal(new BigInteger(4), await pending);
        else
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            Assert.Equal(route == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                failure.Attribution?.Reason);
            if (route == "admission")
            {
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Same(origin, failure.Span);
            }
        }
        Assert.Equal(1, callback.Calls);
        Assert.True(callback.Finished);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class LengthCallback : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Finished;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Unexpected synchronous length callback.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); return new BigInteger(4); }
            finally { Finished = true; }
        }
    }
}
