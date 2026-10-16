using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ChainedComparisonObservationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task NativeResultHonorsStringLimitBeforeTheNextOperand(bool asynchronous, bool chained)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = CreateContext(owner);
        var comparison = new Comparison(PyString.FromString(new string('x', 32)));
        var tail = Prepare(context, comparison);
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => Run(context, asynchronous, chained));
        Assert.Equal(LythonFailureReason.StringLengthLimit, failure.Attribution?.Reason);
        Assert.Equal(0, tail.Calls);
        Assert.Equal(1, comparison.Calls);
        CheckCleanup(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeResultAdmissionFailsBeforeTheNextOperand(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = CreateContext(owner, maximumString: 1000);
        var comparison = new Comparison(PyString.FromString(new string('x', 32)), exhaust: true);
        var tail = Prepare(context, comparison);
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => Run(context, asynchronous, true));
        Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, failure.Attribution?.Reason);
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(0, tail.Calls);
        Assert.Equal(1, comparison.Calls);
        CheckCleanup(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedComparisonKeepsFundingUntilResultValidation(bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = CreateContext(owner, cancellation: cancellation.Token);
        var text = PyString.FromString(new string('x', cancel ? 4 : 32), context.MemoryGovernor, null);
        var comparison = new Comparison(text, suspend: true);
        var tail = Prepare(context, comparison);
        var pending = Run(context, true, true);
        try
        {
            var first = await Task.WhenAny(comparison.Entered.Task, pending).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Same(comparison.Entered.Task, first);
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
            Assert.False(comparison.Finished);
            if (cancel) cancellation.Cancel();
        }
        finally
        {
            comparison.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) { }
        }
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
        Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.StringLengthLimit,
            failure.Attribution?.Reason);
        Assert.True(comparison.Finished);
        Assert.Equal(1, comparison.Calls);
        Assert.Equal(0, tail.Calls);
        Assert.Equal(cancel ? 4 : 32, text.Length);
        CheckCleanup(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static Context CreateContext(ILythonMemoryBudget owner, int maximumString = 8, CancellationToken cancellation = default)
        => new(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, MaxExecutionMemoryBytes = 1024 * 1024, MaxStringLength = maximumString, CancellationToken = cancellation });

    private static Tail Prepare(Context context, Comparison comparison)
    {
        // Use an explicit native lookup, as in the existing operation-boundary fixtures.
        var type = new PyType("Operand", [], new()
        { ["__lt__"] = comparison, ["__getattribute__"] = new Lookup(comparison) }, context.MemoryGovernor, null);
        context.Variables["left"] = new PyInstance(type, context.MemoryGovernor, null);
        var tail = new Tail();
        context.Variables["tail"] = tail;
        return tail;
    }

    private static async Task Run(Context context, bool asynchronous, bool chained)
    {
        var source = chained ? "return left < 1 < tail()" : "return left < 1";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
    }

    private static void CheckCleanup(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Lookup(Comparison comparison) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => Assert.IsType<PyString>(arguments[0].Value).AsString() == "__lt__" ? comparison
                : throw new LythonRuntimeException("AttributeError", "missing test member", span);
    }

    private sealed class Tail : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { Calls++; return new BigInteger(2); }
    }

    private sealed class Comparison(PyString result, bool exhaust = false, bool suspend = false) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal bool Finished;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.False(suspend);
            Calls++;
            if (exhaust)
            {
                var governor = context.MemoryGovernor;
                var bytes = governor.MaxAccountedBytes!.Value - governor.CurrentAccountedBytes;
                governor.Reserve(bytes, span);
                governor.Commit(bytes);
            }
            return result;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => suspend ? AwaitResult() : new(Invoke(arguments, span, context));
        private async ValueTask<object> AwaitResult()
        {
            Calls++;
            Entered.TrySetResult();
            try { await Release.Task.ConfigureAwait(false); return result; }
            finally { Finished = true; }
        }
    }
}
