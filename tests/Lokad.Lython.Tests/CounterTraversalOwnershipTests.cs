using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class CounterTraversalOwnershipTests
{
    [Theory]
    [InlineData(false, "==", "__eq__")] [InlineData(true, "==", "__eq__")]
    [InlineData(false, "<=", "__le__")] [InlineData(true, "<=", "__le__")]
    [InlineData(false, ">=", "__ge__")] [InlineData(true, ">=", "__ge__")]
    public async Task ClearedSourceBackingStaysFundedThroughSuspendedValueComparison(bool truth, string operation, string method)
    {
        const string body = "left.clear()\nright.clear()\nobserve()\nwith open('/hold','w') as stream:stream.write('held')\nreturn True";
        var source = "namespace=globals()\nfrom collections import Counter\n";
        if (truth) source += "class Truth:\n def __bool__(self):\n  " + body.Replace("\n", "\n  ") + "\n";
        source += "class Count:\n def " + method + "(self,other):\n  " +
            (truth ? "return Truth()" : body.Replace("\n", "\n  ")) +
            "\nleft=Counter(a=Count())\nright=Counter(a=Count())\ncapture()\nreturn left" + operation + "right\n";
        var code = Compile(source);
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var host = new DelayedLythonHost();
        var entered = host.PauseWriteUntilCancellation("/hold");
        var context = new Context(host, new LythonRunOptions { MemoryBudget = owner, CancellationToken = cancellation.Token });
        var observation = new Observation();
        context.Variables["capture"] = new Observer(observation, true);
        context.Variables["observe"] = new Observer(observation, false);
        var pending = LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context).AsTask();
        Exception? failure;
        try
        {
            Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Assert.IsType<PyCounter>(context.Variables["left"]).Count);
            Assert.Equal(0, Assert.IsType<PyCounter>(context.Variables["right"]).Count);
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

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeFalseComparisonCannotHideRefusalOrCancellation(bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        { MemoryBudget = owner, CancellationToken = cancellation.Token });
        Setup(context);
        var left = Assert.IsType<PyCounter>(context.Variables["left"]);
        var right = Assert.IsType<PyCounter>(context.Variables["right"]);
        var count = Assert.IsType<PyInstance>(context.Variables["count"]);
        var comparison = new NativeComparison(cancellation, cancel, budget.LimitBytes!.Value);
        Assert.True(count.Type.TrySetMember("__eq__", comparison));
        using (PyStructuralGuard.PushAmbient(context, comparison.Origin))
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => PyEquality.CountersEqual(left, right));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            else
            {
                Assert.Same(comparison.Refusal, failure);
                Assert.Equal(comparison.Origin, failure.Span);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            }
        }
        Assert.Equal(1, comparison.Calls);
        Assert.Equal(1, left.Count);
        Assert.Equal(1, right.Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RetentionRefusalPrecedesValueComparison()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        Setup(context);
        var left = Assert.IsType<PyCounter>(context.Variables["left"]);
        var right = Assert.IsType<PyCounter>(context.Variables["right"]);
        var baseline = budget.ReservedBytes;
        var span = new LythonSourceSpan(31, 4, 2, 1);
        hook.RefuseGrowth = _ => true;
        using (PyStructuralGuard.PushAmbient(context, span))
        {
            var failure = Assert.Throws<LythonRuntimeException>(() => PyEquality.CountersEqual(left, right));
            Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
            Assert.Equal(span, failure.Span);
        }
        Assert.Empty(Assert.IsType<PyList>(context.Variables["effects"]));
        Assert.Equal(1, left.Count);
        Assert.Equal(1, right.Count);
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static void Setup(Context context)
        => LythonRuntime.ExecuteExecutableCodeObject(Compile("namespace=globals()\nfrom collections import Counter\neffects=[]\nclass Count:\n def __eq__(self,other):effects.append('eq');return True\ncount=Count()\nleft=Counter(a=count)\nright=Counter(a=Count())\n"), context, default, null);

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    private sealed class Observation { internal long Expected, AfterClear; }

    private sealed class Observer(Observation observation, bool capture) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            if (capture) observation.Expected = Assert.IsType<PyCounter>(context.Variables["left"]).CommittedStorageBytes;
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
