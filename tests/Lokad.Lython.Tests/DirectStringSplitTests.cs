using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using ICallable = Lokad.Lython.Runtime.LythonRuntime.ICallable;

namespace Lokad.Lython.Tests;

public sealed class DirectStringSplitTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DistinctReceiversProduceOwnedListsWithoutBoundMethodCaches(bool asynchronous, bool separator)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var code = Compile("for text in [left,right]:\n result=text.split(" + (separator ? "','" : "") + ")\n record()");
        context.Variables["left"] = PyString.FromString(separator ? "alpha,beta" : "alpha beta", context.MemoryGovernor, null);
        context.Variables["right"] = PyString.FromString(separator ? "café,😀" : " café\u00a0😀 ", context.MemoryGovernor, null);
        var record = new Record();
        context.Variables["record"] = record;
        _ = await Execute(code, context, asynchronous);
        Assert.Equal(2, record.Values.Count);
        Assert.Equal(new[] { "alpha", "beta" }, record.Values[0].Cast<PyString>().Select(s => s.AsString()));
        Assert.Equal(new[] { "café", "😀" }, record.Values[1].Cast<PyString>().Select(s => s.AsString()));
        Assert.NotSame(record.Values[0], record.Values[1]);
        Assert.All(record.Values, value => Assert.Same(context.MemoryGovernor, value.OwnerMemoryGovernor));
        var caches = (Array)record.Frame!.GetType().GetField("_memberCaches", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(record.Frame)!;
        Assert.All(caches.Cast<object?>(), Assert.Null);
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
            context.Variables["text"] = PyString.FromString("word", context.MemoryGovernor, null);
            var code = Compile("return text.split()");
                var result = Assert.IsType<PyList>(await Execute(code, context, asynchronous));
                Assert.Equal("word", Assert.IsType<PyString>(Assert.Single(result)).AsString());
            AssertUnwound(context);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ResultAdmissionRefusesWithoutChangingTheReceiver(bool asynchronous, bool separator)
    {
                var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var text = PyString.FromString("alpha beta", context.MemoryGovernor, null);
        var baseline = budget.ReservedBytes;
        using (var pad = new Pad(text, budget.LimitBytes!.Value))
        {
            context.Variables["fill"] = pad;
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                await Execute(Compile("text=fill()\nreturn text.split(" + (separator ? "' '" : "") + ")"), context, asynchronous));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Equal(2, failure.Span?.Line);
            Assert.Equal("alpha beta", text.AsString());
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
    [InlineData("success", false, false)]
    [InlineData("cancel", false, false)]
    [InlineData("admission", false, false)]
    [InlineData("success", true, false)]
    [InlineData("cancel", true, false)]
    [InlineData("admission", true, false)]
    [InlineData("success", true, true)]
    [InlineData("cancel", true, true)]
    [InlineData("admission", true, true)]
    public async Task SuspendedReceiverIsNotReplayedAndKeepsItsOwnerUntilCompletion(string route, bool separator, bool argument)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token },regexProvider:Utf8RegexProvider.Instance);
        var text = PyString.FromString("alpha beta", context.MemoryGovernor, null);
        var receiver = new Receiver(argument ? PyString.FromString(" ", context.MemoryGovernor, null) : text);
        context.Variables["receive"] = receiver;
        context.Variables["text"] = text;
        var source = argument ? "return text.split(receive())" : "return receive().split(" + (separator ? "' '" : "") + ")";
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile(source), context).AsTask();
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
        if (route == "success") Assert.Equal(2, Assert.IsType<PyList>(await pending.WaitAsync(TimeSpan.FromSeconds(5))).Count);
        else
        {
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
            if (route == "admission") Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            else Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        }
        Assert.Equal(1, receiver.Calls);
        Assert.Equal("alpha beta", text.AsString());
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
        internal List<PyList> Values = [];
        internal LythonRuntime.ExecutableFrameState? Frame;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Frame = context.CurrentExecutableFrame;
            Assert.True(Frame!.TryResolveOwnLocal("result", out var result));
            Values.Add(Assert.IsType<PyList>(result));
            return PyNone.Instance;
        }
    }

    private sealed class Pad(PyString result, long limit) : ICallable, IDisposable
    {
        private MemoryGovernor.TemporaryMemoryReservation? _storage;
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            _storage = context.MemoryGovernor.ReserveTemporary(limit - context.MemoryGovernor.CurrentAccountedBytes, span);
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
