using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class JsonBooleanOwnershipTests
{
    [Theory]
    [InlineData("success", false)]
    [InlineData("cancel", false)]
    [InlineData("admission", false)]
    [InlineData("success", true)]
    [InlineData("cancel", true)]
    [InlineData("admission", true)]
    public async Task ConversionKeepsItsAliasFundedAndAwaitsAllUsersBeforeCleanup(string route, bool encoding)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, CancellationToken = cancellation.Token });
        var value = PyString.FromString("é", context.MemoryGovernor, null);
        var callback = new ConversionCallback(value);
        context.Variables["hook"] = callback;
        context.Variables["source"] = PyString.FromString("{\"note\":\"a" + char.ToString((char)1) + "b\"}", context.MemoryGovernor, null);
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(
            Compile(encoding ? "import json\nreturn json.dumps({1},default=hook,ensure_ascii=None)"
                : "import json\nreturn json.loads(source,object_hook=hook,strict=None)"), context).AsTask();
        var origin = new LythonSourceSpan(1000, 5, 50, 1);
        try
        {
            var first = await Task.WhenAny(callback.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(callback.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            var held = budget.ReservedBytes;
            Assert.True(held > 0);
            using (var otherOwner = budget.CreateScope())
            {
                var other = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = otherOwner });
                var data = PyString.FromString(new string('x', 1024), other.MemoryGovernor, null);
                other.Variables["data"] = data;
                Assert.Same(data, LythonRuntime.ExecuteExecutableCodeObject(Compile("return data"), other, default, null));
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
            {
                var governor = context.MemoryGovernor;
                _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(
                    governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes + 1, origin));
            }
            Assert.False(callback.Finished);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
        }
        finally
        {
            callback.Release.TrySetResult();
            _ = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        if (route == "success")
        {
            Assert.Equal(encoding ? "\"é\"" : "é", Assert.IsType<PyString>(await pending).AsString());
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
        Assert.Equal(1, callback.Calls);
        Assert.True(callback.Finished);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    private sealed class ConversionCallback(object value) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Finished;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Unexpected synchronous JSON callback.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); return value; }
            finally { Finished = true; }
        }
    }
}
