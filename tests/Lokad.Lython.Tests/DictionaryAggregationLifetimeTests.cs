using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DictionaryAggregationLifetimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedAdditionLeavesExistingDictionaryValueUntouched(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var dictionary = new PyDict(context.MemoryGovernor, null);
        var key = PyString.FromString("x");
        object original = BigInteger.One << 262144;
        dictionary.SetItem(key, original);
        context.Variables["container"] = dictionary;
        context.Variables["selected"] = key;
        context.Variables["number"] = BigInteger.One;
        using var deny = new DenyRemaining(context.MemoryGovernor, limit);
        context.Variables["arm"] = deny;
        var code = Compile("d=container\nkey=selected\namount=number\narm()\nd[key]=d.get(key,0)+amount");
        try
        {
            var failure = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context))
                : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
            Assert.Same(original, dictionary.GetItem(key));
            Assert.Equal(1, dictionary.Count);
            Assert.Null(context.CurrentExecutableFrame);
        }
        finally { deny.Dispose(); }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtocolGetRemainsPendingUntilItsRealUserFinishes(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var tap = new HeldValue();
        var record = new RecordedValue();
        context.Variables["tap"] = tap;
        context.Variables["record"] = record;
        var code = Compile("class D:\n def get(self,key,default): return tap()\n def __setitem__(self,key,value): record(value)\nd=D()\nkey='x'\namount=2\nd[key]=d.get(key,0)+amount");
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        try
        {
            await tap.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            if (cancel) cancellation.Cancel();
            Assert.False(pending.IsCompleted);
            tap.Release.TrySetResult();
            if (cancel)
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
                Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            }
            else await pending;
        }
        finally
        {
            tap.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException failure) when (cancel && failure.Attribution?.Reason == LythonFailureReason.Cancelled) { }
        }
        Assert.Equal(1, tap.Calls);
        if (cancel) Assert.Null(record.Value);
        else Assert.Equal(new BigInteger(7), record.Value);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
    {
        var parsed = LythonFrontend.Compile(source);
        Assert.NotNull(parsed.Script);
        return ExecutableScript.Compile(LoweredScript.Lower(parsed.Script)).EntryPoint;
    }

    private sealed class DenyRemaining(MemoryGovernor governor, long limit) : LythonRuntime.ICallable, IDisposable
    {
        private IDisposable? _reservation;
        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, Context context)
        {
            _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - 64, span);
            return PyNone.Instance;
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, Context context)
            => ValueTask.FromResult(Invoke(args, span, context));
        public void Dispose() { _reservation?.Dispose(); _reservation = null; }
    }

    private sealed class HeldValue : LythonRuntime.ICallable
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, Context context) => throw new InvalidOperationException("Async control.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, Context context)
        {
            Calls++;
            Started.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return new BigInteger(5);
        }
    }

    private sealed class RecordedValue : LythonRuntime.ICallable
    {
        public object? Value { get; private set; }
        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, Context context) { Value = Assert.Single(args).Value; return PyNone.Instance; }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, Context context) => ValueTask.FromResult(Invoke(args, span, context));
    }
}
