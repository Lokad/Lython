using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DirectIntegerTextCallTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task OrdinaryAndGeneralNativeInputsPreserveResultsAndCleanup(bool bytes, bool general, bool asynchronous)
    {
        var code = Compile(general ? "return int(*(value,))" : "return int(value)");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = Prepare(owner);
            Assert.Equal(new BigInteger(7), Assert.IsType<BigInteger>(await Run(code, context, asynchronous)));
            Check(context);
        }
        Assert.Equal(0, budget.ReservedBytes);

        Context Prepare(ILythonMemoryBudget owner)
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner});
            context.Variables["value"] = bytes ? new PyBytes([55], context.MemoryGovernor, null)
                : PyString.FromString("7", context.MemoryGovernor, null);
            return context;
        }
    }

    [Theory]
    [InlineData("success", false)]
    [InlineData("cancel", false)]
    [InlineData("admission", false)]
    [InlineData("success", true)]
    [InlineData("cancel", true)]
    [InlineData("admission", true)]
    public async Task SuspendedInputIsNotReplayedAndKeepsItsOwnerUntilAllUsersFinish(string route, bool bytes)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var payload = new string('9', 101);
        object value = bytes ? new PyBytes(System.Text.Encoding.ASCII.GetBytes(payload), context.MemoryGovernor, null)
            : PyString.FromString(payload, context.MemoryGovernor, null);
        var receiver = new Receiver(value);
        context.Variables["receive"] = receiver;
        var inputFunding = context.MemoryGovernor.CurrentCommittedBytes;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile("return int(receive())"), context).AsTask();
        var origin = new LythonSourceSpan(1000, 5, 50, 1);
        try
        {
            var first = await Task.WhenAny(receiver.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(receiver.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            var held = budget.ReservedBytes;
            Assert.True(held > 0);
            using (var otherOwner = budget.CreateScope())
            {
                var other = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = otherOwner });
                _ = PyString.FromString(new string('x', 1024), other.MemoryGovernor, null);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
                _ = Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.Reserve(
                    ((LythonMemoryBudget)context.MemoryGovernor.Budget!).LimitBytes!.Value - context.MemoryGovernor.CurrentAccountedBytes + 1, origin));
            Assert.False(receiver.Finished);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            receiver.Release.TrySetResult();
            _ = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        if (route == "success")
        {
            var integer = Assert.IsType<BigInteger>(await pending);
            Assert.Equal(BigInteger.Parse(payload), integer);
            Assert.True(context.MemoryGovernor.CurrentCommittedBytes - inputFunding >=
                RuntimeMemoryEstimates.EstimateBigIntegerBytes(integer), "Retained parsed magnitude lost its funding.");
        }
        else
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            Assert.Equal(route == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
            if (route == "admission")
            {
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Same(origin, error.Span);
            }
        }
        Assert.Equal(1, receiver.Calls);
        Assert.True(receiver.Finished);
        Check(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    private static async ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Receiver(object value) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Finished;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
}
