using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Handle = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext.TextFileHandle;

namespace Lokad.Lython.Tests;

public sealed class TextFileWriteCallBoundaryTests(ITestOutputHelper output)
{
    private const int MaximumSteps = 10000;
    private static readonly ExecutableCodeObject Code = Compile("return file.write(argument())");
    // Captured on 2b68d4ea before changing call lowering or runtime dispatch.
    private static readonly string[] Checkpoints =
    [
        "-:0:0", "7,4,1,8:0:0", "7,10,1,8:0:0", "18,8,1,19:0:0",
        "18,10,1,19:0:0", "18,10,1,19:0:0", "18,10,1,19:0:0",
        "7,22,1,8:1:0", "7,22,1,8:1:0", "7,22,1,8:1:0", "7,22,1,8:1:0",
        "0,29,1,1:1:6",
    ];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryFuelCheckpointPreservesArgumentAndStagedOutput(bool asynchronous)
    {
        var checkpoints = new List<string>();
        long steps;
        using (var budget = new LythonMemoryBudget(4 * 1024 * 1024).CreateScope())
        {
            var (context, handle, argument, host) = Prepare(budget);
            try
            {
                Assert.Equal(new BigInteger(3), await Run(context, asynchronous));
                steps = context.Limits.ExecutionStepCount;
                Assert.Equal(1, argument.Calls);
                AssertUnwound(context);
            }
            finally { handle.Exit(); }
            Assert.Equal("a\n😀", host.ReadText("/out"));
        }
        for (var remaining = 0; remaining < steps; remaining++)
        {
            var budget = new LythonMemoryBudget(4 * 1024 * 1024);
            using (var owner = budget.CreateScope())
            {
                var (context, handle, argument, _) = Prepare(owner);
                context.Limits.ExecutionStepCount = MaximumSteps - remaining;
                try
                {
                    var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(context, asynchronous));
                    Assert.Equal(LythonFailureReason.ExecutionStepLimit, error.Attribution?.Reason);
                    Assert.Equal(MaximumSteps + 1, context.Limits.ExecutionStepCount);
                    var span = error.Span is { } location ? $"{location.Start},{location.Length},{location.Line},{location.Column}" : "-";
                    checkpoints.Add($"{span}:{argument.Calls}:{handle.Tell()}");
                    AssertUnwound(context);
                }
                finally
                {
                    // The injected counter is reset only for native fixture cleanup.
                    // No further guest work runs after a failed checkpoint.
                    context.Limits.ExecutionStepCount = 0;
                    handle.Exit();
                }
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(12, steps);
        Assert.Equal(Checkpoints, checkpoints);
        output.WriteLine("TEXT_WRITE_GUARDS:" + JsonSerializer.Serialize(new { asynchronous, steps, checkpoints }));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TypeErrorPrecedesClosedHandleError(bool asynchronous, bool invalidArgument)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, handle, argument, _) = Prepare(owner);
            handle.Exit();
            if (invalidArgument) argument.Value = PyNone.Instance;
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(context, asynchronous));
            Assert.Equal(invalidArgument ? "TypeError" : "ValueError", error.ExceptionType);
            Assert.Equal(invalidArgument ? "file.write(text) expects one string argument." : "I/O operation on closed file", error.Message);
            Assert.Equal(1, argument.Calls);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedWriteLeavesCursorAndRetainedInputUnchanged(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var (context, handle, argument, _) = Prepare(owner);
            var input = PyString.FromString(new string('x', 512), context.MemoryGovernor, null);
            argument.Value = input;
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes - 256, out var padding));
            try
            {
                using (padding)
                {
                    var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(context, asynchronous));
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                    Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                    Assert.True(context.MemoryGovernor.LastDeniedReservationBytes >= 512);
                    Assert.Equal(1, argument.Calls);
                    Assert.Equal(BigInteger.Zero, handle.Tell());
                    Assert.Equal(new string('x', 512), input.AsString());
                    AssertUnwound(context);
                }
            }
            finally { handle.Exit(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedArgumentKeepsReceiverFundedUntilCompletion(bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, handle, argument, host) = Prepare(owner);
            var other = Handle.ForWrite("/other", context);
            argument.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Run(context, true).AsTask();
            try
            {
                Assert.False(pending.IsCompleted);
                Assert.Equal(1, argument.Calls);
                Assert.True(budget.ReservedBytes > 0);
                context.Variables["file"] = other;
                if (cancel)
                {
                    argument.Release.SetCanceled();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                    Assert.Equal(BigInteger.Zero, handle.Tell());
                }
                else
                {
                    argument.Release.SetResult();
                    Assert.Equal(new BigInteger(3), await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                }
                Assert.Equal(BigInteger.Zero, other.Tell());
                AssertUnwound(context);
            }
            finally
            {
                argument.Release.TrySetCanceled();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
                handle.Exit();
                other.Exit();
            }
            Assert.Equal(cancel ? "" : "a\n😀", host.ReadText("/out"));
            Assert.Equal("", host.ReadText("/other"));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static (Context, Handle, Argument, MockLythonHost) Prepare(ILythonMemoryBudget owner)
    {
        var host = new MockLythonHost();
        var context = new Context(host, new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
        var handle = Handle.ForWrite("/out", context);
        var argument = new Argument { Value = PyString.FromString("a\n😀", context.MemoryGovernor, null) };
        context.Variables["file"] = handle;
        context.Variables["argument"] = argument;
        return (context, handle, argument, host);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    private static ValueTask<object?> Run(Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(Code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(Code, context, default, null));

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Argument : LythonRuntime.ICallable
    {
        internal object Value = PyNone.Instance;
        internal int Calls;
        internal TaskCompletionSource? Release;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Calls++; return Value; }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            if (Release is not null) await Release.Task.ConfigureAwait(false);
            return Value;
        }
    }
}
