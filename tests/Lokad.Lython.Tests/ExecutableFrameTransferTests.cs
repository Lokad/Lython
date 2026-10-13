using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableFrameTransferTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackAndSuspensionKeepConsumedOperandsInTheirExecutingFrame(bool asynchronous)
    {
        var frontend = LythonFrontend.Compile("def call():\n    return probe('x' * 16000)\nreturn call()\n");
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var probe = new Probe();
        context.Variables["probe"] = probe;
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context).AsTask();
            await probe.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            probe.Release.SetResult();
            Assert.Equal(new BigInteger(16000), await pending);
        }
        else Assert.Equal(new BigInteger(16000), LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
        Assert.True(probe.SawDynamicCaller);
        Assert.Null(context.State.GetType().GetProperty("ActiveExecutableFrame", Flags)?.GetValue(context.State));
        Assert.NotNull(probe.Frame);
        Assert.Null(probe.Frame.GetType().GetProperty("ActiveCaller", Flags)?.GetValue(probe.Frame));
        var stack = probe.Frame.GetType().GetField("_stack", Flags)!.GetValue(probe.Frame)!;
        var items = (object?[])stack.GetType().GetField("_items", Flags)!.GetValue(stack)!;
        Assert.DoesNotContain(items, item => item is PyString text && text.Utf8Bytes.Length == 16000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObservedResultIsPublishedDuringItsTransientPreflight(bool asynchronous)
    {
        var frontend = LythonFrontend.Compile("return produce()\n");
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var value = new ObservedValue(context);
        context.Variables["produce"] = new Producer(value);
        var result = asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        Assert.Same(value, result);
        Assert.True(value.Observed);
        Assert.Null(context.State.ActiveExecutableFrame);
        Assert.NotNull(value.Frame);
        Assert.Null(value.Frame.PendingObservedValue);
    }

    private sealed class Producer(object value) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context) => value;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedResultPreflightWithdrawsPendingValueAndActiveFrame(bool asynchronous)
    {
        var frontend = LythonFrontend.Compile("return produce()\n");
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = 200 });
        var value = new ObservedValue(context, consumeHeadroom: true);
        context.Variables["produce"] = new Producer(value);
        LythonRuntimeException error;
        if (asynchronous) error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context));
        else error = Assert.Throws<LythonRuntimeException>(() =>
            LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.True(value.Observed);
        Assert.Null(context.State.ActiveExecutableFrame);
        Assert.NotNull(value.Frame);
        Assert.Null(value.Frame.PendingObservedValue);
        Assert.True(value.Frame.RetainedOperands.IsEmpty);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        context.MemoryGovernor.Release(200);
    }

    private sealed class ObservedValue(LythonRuntime.ExecutionContext context, bool consumeHeadroom = false) : IPyGovernedValue
    {
        internal bool Observed;
        internal LythonRuntime.ExecutableFrameState? Frame;
        public MemoryGovernor? OwnerMemoryGovernor
        {
            get
            {
                Frame = context.State.ActiveExecutableFrame;
                Assert.NotNull(Frame);
                Assert.Same(this, Frame.PendingObservedValue);
                if (consumeHeadroom)
                {
                    context.MemoryGovernor.Reserve(200, null);
                    context.MemoryGovernor.Commit(200);
                }
                Observed = true;
                return null;
            }
        }
        public LythonSourceSpan? AllocationSpan => null;
    }

    private sealed class Probe : LythonRuntime.ICallable
    {
        internal readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal object? Frame;
        internal bool SawDynamicCaller;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Check(arguments, context);
            return new BigInteger(16000);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            try { Check(arguments, context); }
            finally { Started.TrySetResult(); }
            await Release.Task.ConfigureAwait(false);
            Check(arguments, context);
            return new BigInteger(16000);
        }

        private void Check(CallArgumentValue[] arguments, LythonRuntime.ExecutionContext context)
        {
            Frame = context.State.GetType().GetProperty("ActiveExecutableFrame", Flags)?.GetValue(context.State)
                ?? context.CurrentExecutableFrame;
            Assert.NotNull(Frame);
            SawDynamicCaller = Frame.GetType().GetProperty("ActiveCaller", Flags)?.GetValue(Frame) is not null;
            var stack = Frame.GetType().GetField("_stack", Flags)!.GetValue(Frame)!;
            var items = (object?[])stack.GetType().GetField("_items", Flags)!.GetValue(stack)!;
            Assert.Contains(items, item => ReferenceEquals(item, this));
            Assert.Contains(items, item => ReferenceEquals(item, arguments[0].Value));
        }
    }
}
