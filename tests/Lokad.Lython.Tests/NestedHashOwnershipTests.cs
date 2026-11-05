using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NestedHashOwnershipTests
{
    [Theory]
    [InlineData("slice", false)] [InlineData("slice", true)]
    [InlineData("dataclass-hash", false)] [InlineData("dataclass-hash", true)]
    [InlineData("dataclass-read", false)] [InlineData("dataclass-read", true)]
    public async Task CaughtCallbackFailureStopsLaterReadsAndHashes(string kind, bool cancel)
    {
        var source = "effects=[]\nclass Key:\n def __hash__(self):\n  arm()\n  return 17\n";
        if (kind == "slice") source += "target=slice(Key(),Key())\n";
        else
        {
            source += "from dataclasses import dataclass\n@dataclass(unsafe_hash=True)\nclass Row:\n a:object\n b:object\n";
            if (kind == "dataclass-read")
                source += " def __getattribute__(self,name):\n  if name=='a':arm()\n  if name=='b':effects.append('later')\n  return object.__getattribute__(self,name)\ntarget=Row(1,2)\n";
            else source += "target=Row(Key(),Key())\n";
        }
        source += "try:hash(target)\nexcept BaseException:effects.append('caught')\nfinally:return 'hidden'\n";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            using var cancellation = new CancellationTokenSource();
            var context = new Context(new MockLythonHost(), new LythonRunOptions
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var arm = new Arm(cancellation, cancel, budget.LimitBytes!.Value);
            context.Variables["arm"] = arm;
            var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
            {
                if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
            });
            Assert.Equal(1, arm.Calls);
            Assert.Empty(Assert.IsType<PyList>(context.Variables["effects"]));
            if (cancel) Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
            else
            {
                Assert.Same(arm.Refusal, failure);
                Assert.Equal(arm.Origin, failure.Span);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            }
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Equal(0, context.State.StructuralTraversal.Depth);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
            Assert.True(budget.ReservedBytes > 0);
            owner.Dispose();
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public void GeneratedFieldSnapshotIsAdmittedBeforeAttributeCallbacks()
    {
        var source = "from dataclasses import dataclass\neffects=[]\n@dataclass(unsafe_hash=True)\nclass Row:\n a:int\n b:int\n def __getattribute__(self,name):\n  if name in ('a','b'):effects.append(name)\n  return object.__getattribute__(self,name)\ntarget=Row(1,2)\n";
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
        var baseline = budget.ReservedBytes;
        hook.RefuseGrowth = bytes => bytes == PyTuple.EstimateApproximateBytes(2);
        var span = new LythonSourceSpan(31, 4, 2, 1);
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            PyHashProtocols.GetProtocolHash(context.Variables["target"], context, span));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(span, failure.Span);
        Assert.Empty(Assert.IsType<PyList>(context.Variables["effects"]));
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Arm(CancellationTokenSource cancellation, bool cancel, long requested) : LythonRuntime.ICallable
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
            return PyNone.Instance;
        }
    }
}
