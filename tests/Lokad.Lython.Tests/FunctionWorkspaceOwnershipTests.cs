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
            try { Assert.All(values, value => Assert.Null(value)); Checked = true; }
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
            first[11] = new object();
            governor.ReturnFunctionWorkspace(first, first.Length);
            Assert.Equal(charge, budget.ReservedBytes);
            Assert.All(first, value => Assert.Null(value));
            var second = governor.RentFunctionWorkspace(12, Span);
            Assert.Same(first, second);
            Assert.Equal(charge, budget.ReservedBytes);
            governor.ReturnFunctionWorkspace(second, second.Length);
            governor.EndFunctionWorkspaceReuse();
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
        first.EndFunctionWorkspaceReuse();
        Assert.Equal(charge, budget.ReservedBytes);
        second.EndFunctionWorkspaceReuse();
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
        active[0] = new object();
        var idle = governor.RentFunctionWorkspace(12, Span);
        Assert.NotSame(active, idle);
        governor.ReturnFunctionWorkspace(idle, idle.Length);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(charge, budget.ReservedBytes);
        Assert.NotNull(active[0]);
        governor.ReturnFunctionWorkspace(active, active.Length);
        Assert.All(active, value => Assert.Null(value));
        Assert.Equal(0, budget.ReservedBytes);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ReturnedCapacityRemainsFundedThroughSmallerBorrow()
    {
        var budget = new LythonMemoryBudget(256);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(owner);
        var first = governor.RentFunctionWorkspace(12, Span);
        governor.ReturnFunctionWorkspace(first, first.Length);
        var second = governor.RentFunctionWorkspace(8, Span);
        var activeCharge = MemoryGovernor.FunctionWorkspaceBytes(second.Length);
        Assert.Equal(activeCharge, budget.ReservedBytes);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(activeCharge, budget.ReservedBytes);
        governor.ReturnFunctionWorkspace(second, second.Length);
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void LogicalBindingsAndRetainedAliasesRemainSeparateFromLoanCapacity()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var frontend = LythonFrontend.Compile("namespace=globals()\ndef identity(value):return value\n");
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!));
        LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
        var function = Assert.IsType<PyExecutableFunction>(context.Variables["identity"]);
        var retained = PyString.FromString(new string('x', 16000), context.MemoryGovernor, Span);
        context.Variables["retained"] = retained;
        var baseline = budget.ReservedBytes;
        var governor = context.MemoryGovernor;
        var previous = governor.RentFunctionWorkspace(64, Span);
        previous[63] = new object();
        governor.ReturnFunctionWorkspace(previous, previous.Length);
        var bound = function.BindPositionalOperands([retained], Span, context);
        Assert.Equal(1, bound.Assigned.Length);
        Assert.Same(retained, bound.Values[0]);
        Assert.All(bound.Values.Skip(1), value => Assert.Null(value));
        var charge = MemoryGovernor.FunctionWorkspaceBytes(bound.Values.Length);
        Assert.Equal(baseline + charge, budget.ReservedBytes);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(baseline + charge, budget.ReservedBytes);
        LythonRuntime.ReturnBoundArguments(bound);
        Assert.All(bound.Values, value => Assert.Null(value));
        Assert.Equal(baseline, budget.ReservedBytes);
        Assert.Same(retained, context.Variables["retained"]);
        Assert.Equal(16000, retained.ByteLength);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void FirstRefusalCannotRecoverThroughCachedCapacityAndCleanupStillRefundsIt()
    {
        var budget = new LythonMemoryBudget(256);
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
        Assert.Equal(256, budget.ReservedBytes);
        governor.EndFunctionWorkspaceReuse();
        Assert.Equal(0, budget.ReservedBytes);
    }
}
