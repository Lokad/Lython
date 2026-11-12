using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class CompletedInvocationLifetimeTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Fact]
    public void RetainedContextHeadroomCoversItsManagedShellAndEmptyNamespace()
    {
        var root = new Context(new MockLythonHost(), null);
        LythonRuntime.ChargeClosureRetention(root, root.MemoryGovernor, Span);
        for (var i = 0; i < 4; i++) _ = new Context(root, ScopeDirectiveFacts.Empty).Variables.Count;
        var beforeAllocation = GC.GetAllocatedBytesForCurrentThread();
        var context = new Context(root, ScopeDirectiveFacts.Empty);
        _ = context.Variables.Count;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
        var beforeCharge = root.MemoryGovernor.CurrentCommittedBytes;
        LythonRuntime.ChargeClosureRetention(context, root.MemoryGovernor, Span);
        var charge = root.MemoryGovernor.CurrentCommittedBytes - beforeCharge;
        Assert.True(charge >= allocated, $"Retained shell allocated {allocated} bytes but acquired {charge} bytes.");
        GC.KeepAlive(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscapingClosureDoesNotRetainRelinquishedInvocationWorkspace(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var (function, root, observer) = Prepare(owner, asynchronous);
        Assert.True(budget.ReservedBytes > 0); // the closure and lexical owner remain funded
        Assert.Null(observer.Context!.CurrentExecutableFrame);
        Assert.Equal(new BigInteger(17), Invoke(function, root, asynchronous));
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
        Assert.False(observer.Workspace!.TryGetTarget(out _));
        Assert.Equal(new BigInteger(17), Invoke(function, root, asynchronous));
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        GC.KeepAlive(observer.Context);
        GC.KeepAlive(function);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static object Invoke(PyFunctionBase function, Context root, bool asynchronous)
        => asynchronous ? function.InvokeAsync([], Span, root).GetAwaiter().GetResult()
            : function.Invoke([], Span, root);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (PyFunctionBase Function, Context Root, Observer Observer) Prepare(ILythonMemoryBudget owner, bool asynchronous)
    {
        var source = """
            def outer():
                marker = 17
                def saved(): return marker
                observe()
                return saved
            return outer()
            """;
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        var root = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var observer = new Observer();
        root.Variables["observe"] = observer;
        var result = asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, root).GetAwaiter().GetResult()
            : LythonRuntime.ExecuteExecutableCodeObject(code, root, default, null);
        root.MemoryGovernor.EndWorkspaceReuse();
        return (Assert.IsAssignableFrom<PyFunctionBase>(result), root, observer);
    }

    private sealed class Observer : LythonRuntime.ICallable
    {
        internal Context? Context;
        internal WeakReference<object[]>? Workspace;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Context = context; // keep the lexical context itself alive after return
            var frame = context.CurrentExecutableFrame!;
            var values = frame._execution.Locals;
            Workspace = new(values);
            return PyNone.Instance;
        }
    }
}
