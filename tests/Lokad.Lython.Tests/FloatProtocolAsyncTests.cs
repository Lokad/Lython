using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class FloatProtocolAsyncTests
{
    [Theory]
    [InlineData("success", false)]
    [InlineData("cancel", false)]
    [InlineData("admission", false)]
    [InlineData("success", true)]
    [InlineData("cancel", true)]
    [InlineData("admission", true)]
    public async Task ConversionKeepsItsAliasFundedAndAwaitsAllUsersBeforeCleanup(string route, bool indexProtocol)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        object value = indexProtocol ? LythonRuntime.OwnFreshInteger(BigInteger.One << 512, context.MemoryGovernor, null) : 7.5;
        var callback = new ConversionCallback(value);
        context.Variables["getnumber"] = callback;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(
            Compile(indexProtocol ? "class Value:\n def __index__(self): return getnumber()\nreturn float(Value())"
                : "class Value:\n def __float__(self): return getnumber()\nreturn float(Value())"), context).AsTask();
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
                var other = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = otherOwner },regexProvider:Utf8RegexProvider.Instance);
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
                    budget.LimitBytes!.Value - governor.CurrentAccountedBytes + 1, origin));
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
            if (indexProtocol) Assert.Equal((double)(BigInteger.One << 512), Assert.IsType<double>(await pending));
            else Assert.Same(value, await pending);
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
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    private sealed class ConversionCallback(object value) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Finished;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => throw new InvalidOperationException("Unexpected synchronous float callback.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); return value; }
            finally { Finished = true; }
        }
    }
}
