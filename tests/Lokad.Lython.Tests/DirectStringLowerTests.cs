using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using ICallable = Lokad.Lython.Runtime.LythonRuntime.ICallable;

namespace Lokad.Lython.Tests;

public class DirectStringLowerTests
{
    // Upper uses the same admission and suspension boundaries, without copying
    // the fixtures or changing the existing lower cases and guard records.
    protected virtual bool Uppercase => false;
    private string Method => Uppercase ? "upper" : "lower";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistinctReceiversProduceOwnedStringsWithoutBoundMethodCaches(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var code = Compile("for text in [left,right]:\n result=text." + Method + "()\n record()");
        context.Variables["left"] = PyString.FromString(Uppercase ? "alpha" : "ALPHA", context.MemoryGovernor, null);
        context.Variables["right"] = PyString.FromString(Uppercase ? "iσß" : "İΣ", context.MemoryGovernor, null);
        var record = new Record();
        context.Variables["record"] = record;
        _ = await Execute(code, context, asynchronous);
        Assert.Equal(2, record.Values.Count);
        Assert.Equal(Uppercase ? "ALPHA" : "alpha", record.Values[0].AsString());
        Assert.Equal(Uppercase ? "IΣSS" : "i\u0307ς", record.Values[1].AsString());
        Assert.NotSame(record.Values[0], record.Values[1]);
        Assert.All(record.Values, value => Assert.Same(context.MemoryGovernor, value.OwnerMemoryGovernor));
        var caches = (Array)record.Frame!.GetType().GetField("_memberCaches", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(record.Frame)!;
        Assert.All(caches.Cast<object?>(), Assert.Null);
        Assert.Equal(0, Assert.Single(code.CallSites, site => site.HasReceiver).ArgumentCount);
        Assert.True(budget.ReservedBytes > 0);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryCallsPreserveResultsAndCleanup(bool asynchronous)
    {
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { },regexProvider:Utf8RegexProvider.Instance);
            context.Variables["text"] = PyString.FromString(Uppercase ? "word" : "WORD", context.MemoryGovernor, null);
            var code = Compile("return text." + Method + "()");
                Assert.Equal(Uppercase ? "WORD" : "word", Assert.IsType<PyString>(await Execute(code, context, asynchronous)).AsString());
            AssertUnwound(context);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ResultAdmissionRefusesWithoutChangingTheReceiver(bool asynchronous, bool exposed)
    {
                const long lookupHeadroom = 128;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var allocationSpan = new LythonSourceSpan(0, 4, 1, 1);
        var source = new string(Uppercase ? 'a' : 'A', 256);
        var text = PyString.FromString(source, context.MemoryGovernor, allocationSpan);
        var baseline = budget.ReservedBytes;
        // Leave control headroom so the refusal belongs to casing storage,
        // including when the method is first exposed as a retainable value.
        using (var pad = new Pad(text, budget.LimitBytes!.Value, lookupHeadroom))
        {
            context.Variables["fill"] = pad;
            var sourceCode = exposed ? "text=fill()\nmethod=text." + Method + "\nreturn method()" : "text=fill()\nreturn text." + Method + "()";
            var code = Compile(sourceCode);
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Execute(code, context, asynchronous));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            var failureSpan = Assert.IsType<LythonSourceSpan>(failure.Span);
            Assert.Equal(exposed ? "method()" : "text." + Method + "()", sourceCode.Substring(failureSpan.Start, failureSpan.Length));
            Assert.Equal(exposed ? 3 : 2, failureSpan.Line);
            Assert.Same(allocationSpan, text.AllocationSpan);
            Assert.True(context.MemoryGovernor.LastDeniedReservationBytes >= text.Utf8Bytes.Length);
            Assert.Equal(source, text.AsString());
            Assert.Equal(1, pad.Calls);
            // The host still owns the pressure reservation until Pad.Dispose.
            Assert.True(context.MemoryGovernor.CurrentReservedBytes > 0);
        }
        AssertUnwound(context);
        Assert.Equal(baseline, budget.ReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("admission")]
    public async Task SuspendedReceiverIsNotReplayedAndKeepsItsOwnerUntilCompletion(string route)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var source = Uppercase ? "alpha beta" : "ALPHA BETA";
        var text = PyString.FromString(source, context.MemoryGovernor, null);
        var receiver = new Receiver(text);
        context.Variables["receive"] = receiver;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile("return receive()." + Method + "()"), context).AsTask();
        try
        {
            await receiver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > 0);
            if (route == "cancel") cancellation.Cancel();
            if (route == "admission")
            {
                var governor = context.MemoryGovernor;
                var fill = budget.LimitBytes!.Value - governor.CurrentAccountedBytes;
                governor.Reserve(fill, null);
                governor.Commit(fill);
                _ = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, null));
            }
        }
        finally
        {
            receiver.Release.TrySetResult();
            _ = await Xunit.Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        if (route == "success") Assert.Equal(Uppercase ? "ALPHA BETA" : "alpha beta", Assert.IsType<PyString>(await pending.WaitAsync(TimeSpan.FromSeconds(5))).AsString());
        else
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            if (route == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            else Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        }
        Assert.Equal(1, receiver.Calls);
        Assert.Equal(source, text.AsString());
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    private static async ValueTask<object?> Execute(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Record : ICallable
    {
        internal List<PyString> Values = [];
        internal LythonRuntime.ExecutableFrameState? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Frame = context.CurrentExecutableFrame;
            Assert.True(Frame!.TryResolveOwnLocal("result", out var result));
            Values.Add(Assert.IsType<PyString>(result));
            return PyNone.Instance;
        }
    }

    private sealed class Pad(PyString result, long limit, long headroom) : ICallable, IDisposable
    {
        private MemoryGovernor.TemporaryMemoryReservation? _storage;
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            _storage = context.MemoryGovernor.ReserveTemporary(limit - headroom - context.MemoryGovernor.CurrentAccountedBytes, span);
            return result;
        }
        public void Dispose() => _storage?.Dispose();
    }

    private sealed class Receiver(PyString result) : ICallable
    {
        internal int Calls;
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context) => throw new InvalidOperationException("Unexpected synchronous receiver.");
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            Entered.TrySetResult();
            await Release.Task.ConfigureAwait(false);
            return result;
        }
    }
}
