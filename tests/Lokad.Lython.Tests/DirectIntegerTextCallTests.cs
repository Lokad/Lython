using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DirectIntegerTextCallTests(ITestOutputHelper output)
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
    public async Task OrdinaryAndGeneralNativeInputsPreserveFuelAndSpans(bool bytes, bool general, bool asynchronous)
    {
        var code = Compile(general ? "return int(*(value,))" : "return int(value)");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        long steps;
        using (var owner = budget.CreateScope())
        {
            var context = Prepare(owner);
            Assert.Equal(new BigInteger(7), Assert.IsType<BigInteger>(await Run(code, context, asynchronous)));
            steps = context.Limits.ExecutionStepCount;
            Check(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        Assert.InRange(steps, 8, 32);
        var checkpoints = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            using (var owner = budget.CreateScope())
            {
                var context = Prepare(owner);
                context.Limits.ExecutionStepCount = 1000 - remaining;
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => { await Run(code, context, asynchronous); });
                Assert.Equal(LythonFailureReason.ExecutionStepLimit, error.Attribution?.Reason);
                Assert.Equal(1001, context.Limits.ExecutionStepCount);
                checkpoints.Add(error.Span is { } s ? $"{s.Start},{s.Length},{s.Line},{s.Column}" : "-");
                Check(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        if (general)
        {
            Assert.Equal(13, steps);
            Assert.Equal(new[] { "-", "7,3,1,8", "7,14,1,8", "13,5,1,14", "12,8,1,13", "12,8,1,13", "7,14,1,8", "12,8,1,13", "12,8,1,13", "7,14,1,8", "7,14,1,8", "7,14,1,8", "0,21,1,1" }, checkpoints);
        }
        else
        {
            Assert.Equal(8, steps);
            Assert.Equal(new[] { "-", "7,3,1,8", "11,5,1,12", "7,10,1,8", "7,10,1,8", "7,10,1,8", "7,10,1,8", "0,17,1,1" }, checkpoints);
        }
        output.WriteLine("DIRECT_INTEGER_TEXT_MATRIX:" + JsonSerializer.Serialize(new { bytes, general, asynchronous, steps, checkpoints }));

        Context Prepare(ILythonMemoryBudget owner)
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = 1000 });
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
            { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, CancellationToken = cancellation.Token });
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
                    context.MemoryGovernor.MaxAccountedBytes!.Value - context.MemoryGovernor.CurrentAccountedBytes + 1, origin));
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
            Assert.Equal(route == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.ExecutionMemoryLimit, error.Attribution?.Reason);
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
