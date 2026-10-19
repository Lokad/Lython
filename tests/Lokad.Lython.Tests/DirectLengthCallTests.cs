using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DirectLengthCallTests
{
    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("admission")]
    public async Task SuspendedInputIsNotReplayedAndRemainsFunded(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var text = PyString.FromString("é😀", context.MemoryGovernor, null);
        var receiver = new Receiver(text);
        context.Variables["receive"] = receiver;
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return len(receive())").Script!)).EntryPoint;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        var origin = new LythonSourceSpan(1000, 5, 50, 1);
        try
        {
            var first = await Task.WhenAny(receiver.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(receiver.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
            {
                var governor = context.MemoryGovernor;
                _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(
                    budget.LimitBytes!.Value - governor.CurrentAccountedBytes + 1, origin));
            }
            Assert.False(receiver.Finished);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            receiver.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) when (route != "success") { }
        }
        if (route == "success") Assert.Equal(new BigInteger(2), await pending);
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
        Assert.Equal(1, receiver.Calls);
        Assert.True(receiver.Finished);
        Assert.Equal("é😀", text.AsString());
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Receiver(PyString value) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Finished;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Unexpected synchronous input.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); return value; }
            finally { Finished = true; }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryLengthGuardsPreserveTheFrozenBoundary(bool asynchronous)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("return len(value)").Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = Prepare(owner);
            Assert.Equal(new BigInteger(2), await Run(context));
            Check(context);
        }
        Assert.Equal(0, budget.ReservedBytes);

        Context Prepare(ILythonMemoryBudget owner)
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
            context.Variables["value"] = PyString.FromString("é😀", context.MemoryGovernor, null);
            return context;
        }
        async ValueTask<object?> Run(Context context) => asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        static void Check(Context context)
        {
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            Assert.Equal(0, context.Limits.CurrentRecursionDepth);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
    }
}
