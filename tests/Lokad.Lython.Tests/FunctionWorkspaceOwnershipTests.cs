using Slot = Lokad.Lython.Runtime.LythonRuntime.ExecutableSlot;
using Lokad.Lython.Runtime;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FunctionWorkspaceOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(7, 4, 2, 3);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeferredInvocationClearsLocalsBeforeReturningCapacity(bool asynchronous)
    {
        var frontend = LythonFrontend.Compile("""
            state = {'value': None}
            def retire(value):
                local = value
                state['value'] = value
                return local
            retire('retained')
            inspect_workspace()
            """);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!));
        using var owner = new LythonMemoryBudget(1024 * 1024).CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var probe = new ClearedWorkspaceProbe();
        context.Variables["inspect_workspace"] = probe;
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
        Assert.True(probe.Checked);
    }

    private sealed class ClearedWorkspaceProbe : LythonRuntime.ICallable
    {
        internal bool Checked;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var values = context.MemoryGovernor.RentFunctionWorkspace(1, span);
            try { Assert.All(values, value => Assert.Null(value.Object)); Checked = true; }
            finally { context.MemoryGovernor.ReturnFunctionWorkspace(values, values.Length); }
            return PyNone.Instance;
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
            => new(Invoke(arguments, span, context));
    }

    [Fact]
    public void SequentialWorkspaceReuseRemainsFundedAndClearedUntilOwnershipEnds()
    {
        var budget = new LythonMemoryBudget(4096);
        using (var owner = budget.CreateScope())
        {
            var governor = new MemoryGovernor(owner);
            var first = governor.RentFunctionWorkspace(12, Span);
            var charge = budget.ReservedBytes;
            Assert.True(charge > 0);
            first[11].Object = new object();
            governor.ReturnFunctionWorkspace(first, first.Length);
            Assert.Equal(charge, budget.ReservedBytes);
            Assert.All(first, value => Assert.Null(value.Object));
            var second = governor.RentFunctionWorkspace(12, Span);
            Assert.Same(first, second);
            Assert.Equal(charge, budget.ReservedBytes);
            governor.ReturnFunctionWorkspace(second, second.Length);
            governor.EndWorkspaceReuse();
            Assert.Equal(0, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ExecutionsSharingAllowanceKeepDistinctWorkspacesAndReturnTheirOwnFunding()
    {
        var budget = new LythonMemoryBudget(4096);
        using var firstOwner = budget.CreateScope();
        using var secondOwner = budget.CreateScope();
        var first = new MemoryGovernor(firstOwner);
        var second = new MemoryGovernor(secondOwner);
        var a = first.RentFunctionWorkspace(12, Span);
        var charge = budget.ReservedBytes;
        first.ReturnFunctionWorkspace(a, a.Length);
        var b = second.RentFunctionWorkspace(12, Span);
        Assert.NotSame(a, b);
        Assert.Equal(2 * charge, budget.ReservedBytes);
        second.ReturnFunctionWorkspace(b, b.Length);
        first.EndWorkspaceReuse();
        Assert.Equal(charge, budget.ReservedBytes);
        second.EndWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ReuseEndingReleasesOnlyIdleStorageAndLateReturnsCannotRecache()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var active = governor.RentFunctionWorkspace(12, Span);
        var charge = budget.ReservedBytes;
        active[0] = Slot.FromObject(new object());
        var idle = governor.RentFunctionWorkspace(12, Span);
        Assert.NotSame(active, idle);
        governor.ReturnFunctionWorkspace(idle, idle.Length);
        governor.EndWorkspaceReuse();
        Assert.Equal(charge, budget.ReservedBytes);
        Assert.NotNull(active[0].Object);
        governor.ReturnFunctionWorkspace(active, active.Length);
        Assert.All(active, value => Assert.Null(value.Object));
        Assert.Equal(0, budget.ReservedBytes);
        governor.EndWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ReturnedCapacityRemainsFundedThroughSmallerBorrow()
    {
        var budget = new LythonMemoryBudget(MemoryGovernor.WorkspaceBytes(12));
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var first = governor.RentFunctionWorkspace(12, Span);
        governor.ReturnFunctionWorkspace(first, first.Length);
        var second = governor.RentFunctionWorkspace(8, Span);
        var activeCharge = MemoryGovernor.WorkspaceBytes(second.Length);
        Assert.Equal(activeCharge, budget.ReservedBytes);
        governor.EndWorkspaceReuse();
        Assert.Equal(activeCharge, budget.ReservedBytes);
        governor.ReturnFunctionWorkspace(second, second.Length);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LogicalBindingsAndRetainedAliasesRemainSeparateFromLoanCapacity(bool mirrored)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var frontend = LythonFrontend.Compile(mirrored
            ? "namespace=globals()\ndef identity(value):\n class Snapshot:\n  saved=value\n return Snapshot.saved\n"
            : "namespace=globals()\ndef identity(value):return value\n");
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!));
        LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
        var function = Assert.IsType<PyExecutableFunction>(context.Variables["identity"]);
        var retained = PyString.FromString(new string('x', 16000), context.MemoryGovernor, Span);
        context.Variables["retained"] = retained;
        var baseline = budget.ReservedBytes;
        var governor = context.MemoryGovernor;
        var previous = governor.RentFunctionWorkspace(64, Span);
        previous[63] = Slot.FromObject(new object());
        governor.ReturnFunctionWorkspace(previous, previous.Length);
        var bound = function.BindCallOperands([Slot.FromObject(retained)], Span, context, receiver: null, callSite: null);
        Assert.Equal(1, bound.ParameterCount);
        Assert.True(bound.Values.Length >= function.CodeObject.ValueStorageCapacity);
        Assert.Same(retained, bound.Values[0].Object);
        Assert.All(bound.Values.Skip(1), value => Assert.Null(value.Object));
        var charge = MemoryGovernor.WorkspaceBytes(bound.Values.Length);
        Assert.Equal(baseline + charge, budget.ReservedBytes);
        governor.EndWorkspaceReuse();
        Assert.Equal(baseline + charge, budget.ReservedBytes);
        LythonRuntime.ReturnBoundArguments(bound);
        Assert.All(bound.Values, value => Assert.Null(value.Object));
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Same(retained, context.Variables["retained"]);
        Assert.Equal(16000, retained.ByteLength);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void FirstRefusalCannotRecoverThroughCachedCapacityAndCleanupStillRefundsIt()
    {
        var budget = new LythonMemoryBudget(MemoryGovernor.WorkspaceBytes(12));
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var cached = governor.RentFunctionWorkspace(12, Span);
        governor.ReturnFunctionWorkspace(cached, cached.Length);
        var first = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(1, Span));
        var again = Assert.Throws<LythonRuntimeException>(() => governor.RentFunctionWorkspace(8, Span));
        Assert.Same(first, again);
        Assert.Same(first, Assert.Throws<LythonRuntimeException>(() => governor.RentFunctionWorkspace(0, Span)));
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, first.Attribution?.Reason);
        Assert.Equal(1, first.Attribution?.Requested);
        Assert.Equal(Span, first.Span);
        Assert.Equal(MemoryGovernor.WorkspaceBytes(12), budget.ReservedBytes);
        governor.EndWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedLoanPreservesActiveArgumentsAndTheFirstOrigin()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
        var governor = context.MemoryGovernor;
        var baseline = budget.ReservedBytes;
        var active = governor.RentFunctionWorkspace(32, Span, context);
        active[0].Object = new System.Numerics.BigInteger(37);
        var charge = budget.ReservedBytes;
        hook.RefuseGrowth = _ => true;
        var first = Assert.Throws<LythonRuntimeException>(() => governor.RentFunctionWorkspace(64, Span, context));
        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, first.Attribution?.Reason);
        Assert.Same(Span, first.Span);
        Assert.Same(first, Assert.Throws<LythonRuntimeException>(() => governor.RentFunctionWorkspace(32, Span, context)));
        Assert.Equal(charge, budget.ReservedBytes);
        Assert.Equal(new System.Numerics.BigInteger(37), Assert.IsType<System.Numerics.BigInteger>(active[0].Object));
        governor.ReturnFunctionWorkspace(active, active.Length);
        governor.EndWorkspaceReuse();
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Fact]
    public void CancellationDuringAdmissionUnwindsUnallocatedFunctionSlots()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new() { MemoryBudget = hook, CancellationToken = cancellation.Token });
        var baseline = budget.ReservedBytes;
        hook.AfterGrowth = _ => cancellation.Cancel();
        var failure = Assert.Throws<LythonRuntimeException>(() =>
            context.MemoryGovernor.RentFunctionWorkspace(32, Span, context));
        Assert.Equal(LythonFailureReason.Cancelled, failure.Attribution?.Reason);
        Assert.Same(Span, failure.Span);
        Assert.Equal(baseline, budget.ReservedBytes);
        context.MemoryGovernor.EndWorkspaceReuse();
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}
