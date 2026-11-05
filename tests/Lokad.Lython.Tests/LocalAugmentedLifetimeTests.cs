using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class LocalAugmentedLifetimeTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task OriginalOperandSurvivesProtocolRebindingAndAwait(bool asynchronous, bool cancel, bool refuse)
    {
        const string source = """
            class Value:
             def __iadd__(self,other):
              hold(other)
              global rhs
              rhs=['changed']
              return other
            rhs=['original']
            previous=rhs
            left=Value()
            observe()
            left+=rhs
            assert left is previous
            return rhs is previous
            """;
        var frontend = LythonFrontend.Compile(source);
        Assert.NotNull(frontend.Script);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script)).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var observe = new Observe();
        var hold = new Hold(observe, refuse);
        context.Variables["observe"] = observe;
        context.Variables["hold"] = hold;
        if (asynchronous)
        {
            var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
            try
            {
                await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                var funded = budget.ReservedBytes;
                Assert.True(funded > 0);
                if (cancel) cancellation.Cancel();
                Assert.False(pending.IsCompleted);
                Assert.Equal(funded, budget.ReservedBytes);
            }
            finally
            {
                hold.Release.TrySetResult();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (LythonRuntimeException) when (cancel || refuse) { }
            }
            if (cancel || refuse)
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
                if (refuse) Assert.Same(hold.Refusal, error);
                else Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                Assert.True(observe.Frame!.TryResolveOwnLocal("left", out var left));
                Assert.Same(observe.Left, left);
            }
            else Assert.Equal(false, await pending);
        }
        else Assert.Equal(false, LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
        Assert.True(hold.Finished);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("produce(rhs)", false, false)]
    [InlineData("hold(rhs)", false, false)]
    [InlineData("Source()[0]", false, false)]
    [InlineData("produce(rhs)", true, false)]
    [InlineData("hold(rhs)", true, false)]
    [InlineData("produce(rhs)", false, true)]
    public async Task OrdinaryResultWaitsForProducerAndPreservesTarget(string expression, bool cancel, bool refuse)
    {
        var source = """
            def produce(other):
             hold(other)
             return other
            class Source:
             def __getitem__(self,key):return produce(rhs)
            rhs=['original']
            previous=rhs
            left=['target']
            observe()
            left=
            """.TrimEnd() + expression + "\nreturn left is previous\n";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var observe = new Observe();
        var hold = new Hold(observe, refuse);
        context.Variables["observe"] = observe;
        context.Variables["hold"] = hold;
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        try
        {
            await hold.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(pending.IsCompleted);
            Assert.True(observe.Frame!.TryResolveOwnLocal("left", out var left));
            Assert.Same(observe.Left, left);
            var funded = budget.ReservedBytes;
            Assert.True(funded > 0);
            if (cancel) cancellation.Cancel();
            Assert.False(pending.IsCompleted);
            Assert.Equal(funded, budget.ReservedBytes);
        }
        finally
        {
            hold.Release.TrySetResult();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (LythonRuntimeException) when (cancel || refuse) { }
        }
        if (cancel || refuse)
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => pending);
            if (refuse) Assert.Same(hold.Refusal, error);
            else Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
            Assert.True(observe.Frame!.TryResolveOwnLocal("left", out var left));
            Assert.Same(observe.Left, left);
        }
        else Assert.Equal(true, await pending);
        Assert.True(hold.Finished);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Observe : LythonRuntime.ICallable
    {
        internal LythonRuntime.ExecutableFrameState? Frame;
        internal object? Left, Right;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Frame = Assert.IsAssignableFrom<LythonRuntime.ExecutableFrameState>(context.CurrentExecutableFrame);
            Assert.True(Frame.TryResolveOwnLocal("left", out Left));
            Assert.True(Frame.TryResolveOwnLocal("rhs", out Right));
            return PyNone.Instance;
        }
    }

    private sealed class Hold(Observe observed, bool refuse) : LythonRuntime.ICallable
    {
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Finished;
        internal LythonRuntimeException? Refusal;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Single(arguments);
            Assert.Same(observed.Right, arguments[0].Value);
            Assert.Equal(PyString.FromString("original"), Assert.Single(Assert.IsType<PyList>(arguments[0].Value)));
            if (refuse) Refusal = context.MemoryGovernor.RefuseAdmission("suspended update", 777, span);
            Finished = true;
            return arguments[0].Value;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            try
            {
                Entered.TrySetResult();
                await Release.Task.ConfigureAwait(false);
                return Invoke(arguments, span, context);
            }
            finally { Finished = true; Entered.TrySetResult(); }
        }
    }
}
