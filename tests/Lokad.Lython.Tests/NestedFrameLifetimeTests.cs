using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NestedFrameLifetimeTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ObservedParentAndEscapingCellSurviveWideChild(bool asynchronous, bool cancel)
    {
        var parameters = string.Join(",", Enumerable.Range(0, 64).Select(i => "p" + i));
        var arguments = string.Join(",", Enumerable.Range(0, 64));
        var source = "def outer(seed):\n" +
            " marker=seed\n" +
            " def retained():\n  return marker\n" +
            " def wide(" + parameters + "):\n" +
            "  nonlocal marker\n  marker=marker+1\n  hold(p0,p63)\n  return marker\n" +
            " observe()\n value=wide(" + arguments + ")\n" +
            " assert value==seed+1\n assert marker==seed+1\n return retained\n" +
            "result=outer(9)\nreturn result()\n";
        var frontend = LythonFrontend.Compile(source);
        Assert.NotNull(frontend.Script);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script)).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var observe = new Observe();
        var hold = new Hold(observe);
        context.Variables["observe"] = observe;
        context.Variables["hold"] = hold;
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
            try
            {
                await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > 0);
                var live = budget.ReservedBytes;
                hold.CheckParent();
                if (cancel) cancellation.Cancel();
                // Cancellation cannot end the owner before the real user exits.
                Assert.False(pending.IsCompleted);
                Assert.Equal(live, budget.ReservedBytes);
            }
            finally
            {
                hold.Release.TrySetResult();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (LythonRuntimeException) when (cancel) { }
            }
            if (cancel)
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
                Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            }
            else Assert.Equal(new BigInteger(10), await pending);
        }
        else Assert.Equal(new BigInteger(10), LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
        Assert.True(hold.Finished);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Null(observe.Context!.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Observe : LythonRuntime.ICallable
    {
        internal Context? Context;
        internal LythonRuntime.ExecutionContext? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Context = context;
            Frame = Assert.IsAssignableFrom<LythonRuntime.ExecutionContext>(context.CurrentExecutableFrame);
            Assert.True(Frame.TryReadExecutableLocal("seed", out var seed));
            Assert.Equal(new BigInteger(9), seed);
            return PyNone.Instance;
        }
    }

    private sealed class Hold(Observe parent) : LythonRuntime.ICallable
    {
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Finished;
        internal void CheckParent()
        {
            Assert.Same(parent.Frame, parent.Context!.CurrentExecutableFrame);
            Assert.True(parent.Frame!.TryReadExecutableLocal("seed", out var seed));
            Assert.Equal(new BigInteger(9), seed);
            Assert.True(parent.Frame.TryReadExecutableLocal("marker", out var marker));
            Assert.Equal(new BigInteger(10), marker);
        }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Equal(2, arguments.Length);
            Assert.Equal(BigInteger.Zero, arguments[0].Value);
            Assert.Equal(new BigInteger(63), arguments[1].Value);
            CheckParent();
            Finished = true;
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            try
            {
                CheckParent();
                Entered.TrySetResult();
                await Release.Task.ConfigureAwait(false);
                return Invoke(arguments, span, context);
            }
            finally { Finished = true; Entered.TrySetResult(); }
        }
    }
}
