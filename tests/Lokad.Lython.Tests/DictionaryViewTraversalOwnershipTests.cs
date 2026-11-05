using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class DictionaryViewTraversalOwnershipTests
{
    [Theory]
    [InlineData(false, "left.items()==right.items()")] [InlineData(true, "left.items()==right.items()")]
    [InlineData(false, "left.items()<=right.items()")] [InlineData(true, "left.items()<=right.items()")]
    [InlineData(false, "left.items().__eq__(right.items())")] [InlineData(true, "left.items().__eq__(right.items())")]
    [InlineData(false, "left.items().__le__(right.items())")] [InlineData(true, "left.items().__le__(right.items())")]
    public async Task ClearedBackingStaysFundedThroughSuspendedValueComparison(bool truth, string expression)
    {
        const string body = "left.clear()\nright.clear()\nobserve()\nwith open('/hold','w') as stream:stream.write('held')\nreturn True";
        var source = "namespace=globals()\n";
        if (truth) source += "class Truth:\n def __bool__(self):\n  " + body.Replace("\n", "\n  ") + "\n";
        source += "class Value:\n def __eq__(self,other):\n  " +
            (truth ? "return Truth()" : body.Replace("\n", "\n  ")) +
            "\nleft={i:Value() for i in range(32)}\nright={i:Value() for i in range(32)}\ncapture()\nreturn " + expression + "\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        var entered = host.PauseWriteUntilCancellation("/hold");
        var context = new Context(host, new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var observation = new Observation();
        context.Variables["capture"] = new Observer(observation, true);
        context.Variables["observe"] = new Observer(observation, false);
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile(source), context).AsTask();
        Exception? failure;
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Assert.IsType<PyDict>(context.Variables["left"]).Count);
            Assert.Equal(0, Assert.IsType<PyDict>(context.Variables["right"]).Count);
            Assert.True(observation.Expected > 0);
            Assert.True(observation.AfterClear >= observation.Expected,
                $"Old backing {observation.Expected} was not funded before I/O: {observation.AfterClear}");
        }
        finally
        {
            cancellation.Cancel();
            failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.Equal(LythonFailureReason.Cancelled, Assert.IsType<LythonRuntimeException>(failure).Attribution?.Reason);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        Assert.Null(context.CurrentExecutableFrame);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task NativeKeyViewMembershipAwaitsStoredKeyEquality()
    {
        const string source = "namespace=globals()\nclass Key:\n def __hash__(self):return hash('needle')\n def __eq__(self,other):\n  source.clear()\n  with open('/hold','w') as stream:stream.write('held')\n  return True\nsource={Key():1}\nreturn 'needle' in source.keys()\n";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        var entered = host.PauseWriteUntilCancellation("/hold");
        var context = new Context(host, new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(Compile(source), context).AsTask();
        Exception? failure;
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Assert.IsType<PyDict>(context.Variables["source"]).Count);
        }
        finally
        {
            cancellation.Cancel();
            failure = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.Equal(LythonFailureReason.Cancelled, Assert.IsType<LythonRuntimeException>(failure).Attribution?.Reason);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        Assert.Null(context.CurrentExecutableFrame);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FalseNativeValueCannotHideRefusalOrCancellation(bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        Setup(context);
        var left = Assert.IsType<PyDict>(context.Variables["left"]);
        var right = Assert.IsType<PyDict>(context.Variables["right"]);
        var value = Assert.IsType<PyInstance>(context.Variables["value"]);
        var comparison = new NativeComparison(cancellation, cancel, budget.LimitBytes!.Value);
        Assert.True(value.Type.TrySetMember("__eq__", comparison));
        var failure = Assert.Throws<LythonRuntimeException>(() => PyContainment.CompareSetViews(
            new LythonRuntime.DictItemsView(left), new LythonRuntime.DictItemsView(right), "__eq__", context, comparison.Origin));
        if (cancel) Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        else
        {
            Assert.Same(comparison.Refusal, failure);
            Assert.Equal(comparison.Origin, failure.Span);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
        }
        Assert.Equal(1, comparison.Calls);
        Assert.Equal(1, left.Count);
        Assert.Equal(1, right.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RetentionRefusalPrecedesValueEffects()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        Setup(context);
        var left = Assert.IsType<PyDict>(context.Variables["left"]);
        var right = Assert.IsType<PyDict>(context.Variables["right"]);
        var baseline = budget.ReservedBytes;
        var span = new LythonSourceSpan(31, 4, 2, 1);
        hook.RefuseGrowth = _ => true;
        var failure = Assert.Throws<LythonRuntimeException>(() => PyContainment.CompareSetViews(
            new LythonRuntime.DictItemsView(left), new LythonRuntime.DictItemsView(right), "__eq__", context, span));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(span, failure.Span);
        Assert.Empty(Assert.IsType<PyList>(context.Variables["effects"]));
        Assert.Equal(1, left.Count);
        Assert.Equal(1, right.Count);
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task ExplicitContainsCannotHideRefusalOrCancellation(bool cancel, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        Setup(context);
        LythonRuntime.ExecuteExecutableCodeObject(Compile("from collections import ChainMap\nview=ChainMap(left).items()\n"), context, default, null);
        var left = Assert.IsType<PyDict>(context.Variables["left"]);
        var right = Assert.IsType<PyDict>(context.Variables["right"]);
        var value = Assert.IsType<PyInstance>(context.Variables["value"]);
        var comparison = new NativeComparison(cancellation, cancel, budget.LimitBytes!.Value);
        Assert.True(value.Type.TrySetMember("__eq__", comparison));
        var pair = PyTuple.FromOwnedArray([left.Keys.Single(), right.Values.Single()]);
        Assert.True(PyMemberAccess.TryResolve(context.Variables["view"], "__contains__", context, comparison.Origin, out var callable));
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (asynchronous) await CallableInvocation.InvokeUnaryAsync(callable, pair, comparison.Origin, context);
            else CallableInvocation.InvokeUnary(callable, pair, comparison.Origin, context);
        });
        var runtimeFailure = Assert.IsType<LythonRuntimeException>(failure);
        if (cancel) Assert.Equal(LythonFailureReason.Cancelled, runtimeFailure.Attribution?.Reason);
        else
        {
            Assert.Same(comparison.Refusal, runtimeFailure);
            Assert.Equal(comparison.Origin, runtimeFailure.Span);
        }
        Assert.Equal(1, comparison.Calls);
        Assert.Equal(1, left.Count);
        Assert.Equal(1, right.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static void Setup(Context context)
        => LythonRuntime.ExecuteExecutableCodeObject(Compile("namespace=globals()\neffects=[]\nclass Value:\n def __eq__(self,other):effects.append('eq');return True\nvalue=Value()\nleft={'a':value}\nright={'a':Value()}\n"), context, default, null);

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    private sealed class Observation { internal long Expected, AfterClear; }

    private sealed class Observer(Observation observation, bool capture) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            if (capture) observation.Expected = Assert.IsType<PyDict>(context.Variables["left"]).CommittedStorageBytes;
            else observation.AfterClear = context.MemoryGovernor.CurrentReservedBytes;
            return PyNone.Instance;
        }
    }

    private sealed class NativeComparison(CancellationTokenSource cancellation, bool cancel, long requested) : LythonRuntime.ICallable
    {
        internal int Calls;
        internal LythonRuntimeException? Refusal;
        internal LythonSourceSpan Origin = new(31, 4, 2, 1);
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            if (cancel) cancellation.Cancel();
            else
            {
                try { context.MemoryGovernor.Reserve(requested, Origin); }
                catch (LythonRuntimeException error) { Refusal = error; }
            }
            return false;
        }
    }
}
