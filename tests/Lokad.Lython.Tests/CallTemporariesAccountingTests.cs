using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: per-call variadic materializations (keyword dicts plus key strings,
/// overflow lists and tuples) register in the run call-temporaries pool, so
/// dropping them across thousands of binds reclaims to crumbs while the
/// bound-call sweep cadence keeps peaks flat. Deterministic via explicit
/// collection plus a full drain.
/// </summary>
public sealed class CallTemporariesAccountingTests
{
    [Fact]
    public void DroppedKwargsDictsReclaim()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 30000000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var plan = new FunctionBindingPlan(
            "f",
            PythonCallableKind.Function,
            [new LoweredFunctionParameter("k", FunctionParameterKind.VariadicDictionary, null, null)],
            new Dictionary<string, object>());
        AbandonKwargsBinds(context, plan, span);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        Assert.True(committed < 100000, $"committed={committed} reserved={reserved}");
        Assert.True(reserved < 100000, $"committed={committed} reserved={reserved}");
    }

    [Fact]
    public void DroppedOverflowTuplesReclaim()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MaxExecutionMemoryBytes = 30000000 });
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var plan = new FunctionBindingPlan(
            "f",
            PythonCallableKind.Function,
            [new LoweredFunctionParameter("a", FunctionParameterKind.VariadicList, null, null)],
            new Dictionary<string, object>());
        AbandonStarargsBinds(context, plan, span);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        context.State.CallTemporaries.Sweep(full: true);
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        var reserved = context.MemoryGovernor.CurrentReservedBytes;
        Assert.True(committed < 100000, $"committed={committed} reserved={reserved}");
        Assert.True(reserved < 100000, $"committed={committed} reserved={reserved}");
    }

// All bound temporaries die with this frame, so no test slots root them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonKwargsBinds(
        LythonRuntime.ExecutionContext context,
        FunctionBindingPlan plan,
        LythonSourceSpan span)
    {
        for (var i = 0; i < 2000; i++)
        {
            LythonRuntime.BindFunctionArguments(
                [CallArgumentValue.Keyword("a", PyNone.Instance), CallArgumentValue.Keyword("b", PyNone.Instance)],
                span,
                plan,
                context);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonStarargsBinds(
        LythonRuntime.ExecutionContext context,
        FunctionBindingPlan plan,
        LythonSourceSpan span)
    {
        for (var i = 0; i < 2000; i++)
        {
            LythonRuntime.BindFunctionArguments(
                [CallArgumentValue.Positional(PyNone.Instance), CallArgumentValue.Positional(PyNone.Instance)],
                span,
                plan,
                context);
        }
    }
}