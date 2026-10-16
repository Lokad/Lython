using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class OwnedFunctionLocalsTests
{
    private static readonly LythonSourceSpan Span = new(40, 3, 4, 2);
    private const string Definition = "def f(arg):\n first=arg\n second=first\n third=second\n tap()\n return third";

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task SequentialCallsReturnWorkingCapacityWithoutCollecting(int entry, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var tap = new Tap();
        context.Variables["tap"] = tap;
        var function = Define(context);
        var value = PyString.FromString("retained", context.MemoryGovernor, null);
        var baseline = budget.ReservedBytes;
        for (var i = 0; i < 16; i++)
        {
            var result = await Invoke(function, value, context, entry, asynchronous);
            Assert.Same(value, result);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.All(tap.Storage!, Assert.Null);
            Check(context);
        }
        Assert.Equal(16, tap.Calls);
        Assert.Equal("retained", value.AsString());
        Assert.True(baseline > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task BufferAdmissionPrecedesBindingErrorsAndBodyEffects(bool sharedLimit, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024 });
        var tap = new Tap();
        context.Variables["tap"] = tap;
        var function = Define(context);
        var governor = context.MemoryGovernor;
        ILythonMemoryReservation? blocker = null;
        if (sharedLimit)
            Assert.True(budget.TryReserve(budget.LimitBytes!.Value - budget.ReservedBytes, out blocker));
        else
        {
            var remaining = governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes;
            governor.Reserve(remaining, Span);
            governor.Commit(remaining);
        }
        using (blocker)
        {
            var baseline = budget.ReservedBytes;
            // This is also an invalid keyword. Major capacity must be admitted
            // before binding can issue TypeError or enter the body.
            CallArgumentValue[] arguments = [CallArgumentValue.Keyword("unknown", PyNone.Instance)];
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await function.InvokeAsync(arguments, Span, context);
                else function.Invoke(arguments, Span, context);
            });
            Assert.Equal(sharedLimit ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.ExecutionMemoryLimit,
                error.Attribution?.Reason);
            Assert.Same(governor.AdmissionFailure, error);
            Assert.Same(Span, error.Span);
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(0, tap.Calls);
            Check(context);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task BindingAndFrameEntryFailuresReturnWorkingCapacity(int failure, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, MaxRecursionDepth = failure == 3 ? 0 : 100 });
        var tap = new Tap();
        context.Variables["tap"] = tap;
        var function = Define(context);
        var baseline = budget.ReservedBytes;
        CallArgumentValue[] arguments = failure switch
        {
            0 => [],
            1 => [CallArgumentValue.Positional(PyNone.Instance), CallArgumentValue.Positional(PyNone.Instance)],
            2 => [CallArgumentValue.Keyword("unknown", PyNone.Instance)],
            _ => [CallArgumentValue.Positional(PyNone.Instance)]
        };
        var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
        {
            if (asynchronous) await function.InvokeAsync(arguments, Span, context);
            else function.Invoke(arguments, Span, context);
        });
        Assert.Equal(failure == 3 ? "RecursionError" : "TypeError", error.ExceptionType);
        Assert.Equal(0, tap.Calls);
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Null(context.MemoryGovernor.AdmissionFailure);
        Check(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("admission")]
    public async Task SuspendedBodyKeepsCapacityUntilItsLastUserFinishes(string route)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, CancellationToken = cancellation.Token });
        var tap = new Tap { Suspend = true };
        context.Variables["tap"] = tap;
        var function = Define(context);
        var value = PyString.FromString("retained", context.MemoryGovernor, null);
        var baseline = budget.ReservedBytes;
        var pending = function.InvokeAsync([CallArgumentValue.Positional(value)], Span, context).AsTask();
        try
        {
            await tap.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            Assert.All(tap.Storage!, item => Assert.Same(value, item));
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
                Assert.Throws<LythonRuntimeException>(() => context.MemoryGovernor.Reserve(
                    context.MemoryGovernor.MaxAccountedBytes!.Value - context.MemoryGovernor.CurrentAccountedBytes + 1, Span));
        }
        finally
        {
            tap.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) when (route != "success") { }
        }
        if (route == "success") Assert.Same(value, await pending);
        else
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            Assert.Equal(route == "cancel" ? LythonFailureReason.Cancelled : LythonFailureReason.ExecutionMemoryLimit,
                error.Attribution?.Reason);
            if (route == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
        }
        Assert.True(tap.Finished);
        Assert.All(tap.Storage!, Assert.Null);
        Assert.Equal(baseline, budget.ReservedBytes);
        Check(context);
        Assert.Equal("retained", value.AsString());
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static PyExecutableFunction Define(Context context)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(Definition).Script!)).EntryPoint;
        LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        return Assert.IsType<PyExecutableFunction>(context.Variables["f"]);
    }

    private static async ValueTask<object> Invoke(PyExecutableFunction f, object value, Context context, int entry, bool asynchronous)
    {
        var bound = entry >= 2;
        CallArgumentValue[] arguments = bound ? [] : [CallArgumentValue.Positional(value)];
        object?[] operands = bound ? [] : [value];
        if (asynchronous) return entry switch
        {
            0 => await f.InvokeAsync(arguments, Span, context),
            1 => await f.InvokeFromOperandsAsync(operands, Span, context),
            2 => await f.InvokeAsync(arguments, Span, context, value),
            _ => await f.InvokeFromOperandsAsync(operands, Span, context, value)
        };
        return entry switch
        {
            0 => f.Invoke(arguments, Span, context),
            1 => f.InvokeFromOperands(operands, Span, context),
            2 => f.Invoke(arguments, Span, context, value),
            _ => f.InvokeFromOperands(operands, Span, context, value)
        };
    }

    private static void Check(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Tap : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Suspend;
        internal bool Finished;
        internal object[]? Storage;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Storage = (object[])typeof(LythonRuntime.ExecutableFrameState)
                .GetField("_locals", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(context.CurrentExecutableFrame)!;
            Assert.Equal(4, Storage.Length);
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var result = Invoke(arguments, span, context);
            Entered.TrySetResult();
            try { if (Suspend) await Release.Task.ConfigureAwait(false); return result; }
            finally { Finished = true; }
        }
    }
}
