using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ComposedNativeCallTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 100, 1);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComposedNumericHelpersNeedNoInvocationStorage(bool named)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            Define(context, """
                def round_amount(amount,rate=2000):
                    return (amount*rate+5000)//10000
                def price(amount):
                    net=round_amount(rate=9000,amount=amount)
                    return net+round_amount(net)
                """);
            var function = Assert.IsType<PyExecutableFunction>(context.Variables["price"]);
            var call = Assert.Single(Compile(named ? "price(amount=1250)" : "price(1250)").CallSites);
            var baseline = budget.ReservedBytes;
            hook.RefuseGrowth = _ => true;
            try
            {
                Assert.True(function.TryStartNativeCall(call, [new BigInteger(1250)], null, context, Span,
                    false, !named, default, out _, out var result, out var frame));
                Assert.Null(frame);
                Assert.Equal(new BigInteger(1350), result);
                Assert.Null(context.MemoryGovernor.AdmissionFailure);
                Assert.Equal(baseline, budget.ReservedBytes);
                AssertBalanced(context);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedParentPromotionUnwindsPreparedDescendants(int admittedWorkspaces)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var inputs = budget.CreateScope();
        var retained = PyString.FromString(new string('A', 8192), new MemoryGovernor(inputs), Span);
        var inputCharge = budget.ReservedBytes;
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var tap = new Tap();
            context.Variables["tap"] = tap;
            Define(context, """
                def leaf(value):
                    return tap(value)
                def middle(value):
                    a=1
                    b=2
                    c=3
                    return leaf(value)+a+b+c
                def outer(value):
                    a=1
                    b=2
                    c=3
                    d=4
                    e=5
                    f=6
                    g=7
                    return middle(value)+a+b+c+d+e+f+g
                """);
            var function = Assert.IsType<PyExecutableFunction>(context.Variables["outer"]);
            Assert.True(function.CanStartInline);
            Assert.True(Assert.IsType<PyExecutableFunction>(context.Variables["middle"]).CanStartInline);
            var call = Assert.Single(Compile("outer(7)").CallSites);
            var growths = 0;
            // Refuse a parent workspace after one or two real descendants have
            // acquired theirs. This counts admissions, never execution checkpoints.
            hook.RefuseGrowth = _ => growths >= admittedWorkspaces;
            hook.AfterGrowth = _ => growths++;
            try
            {
                var failure = Assert.Throws<LythonRuntimeException>(() => function.TryStartNativeCall(call,
                    [retained], null, context, Span, false, true, default, out _, out _, out _));
                Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Equal(admittedWorkspaces, growths);
                Assert.Equal(0, tap.Calls);
                AssertBalanced(context);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                hook.AfterGrowth = null;
                hook.RefuseGrowth = null;
                context.State.CloseResources();
            }
        }
        Assert.Equal(inputCharge, budget.ReservedBytes);
        Assert.Equal(new string('A', 8192), retained.AsString());
        inputs.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
    }

    private static void Define(LythonRuntime.ExecutionContext context, string source)
        => LythonRuntime.ExecuteExecutableCodeObject(Compile(source), context);

    private static void AssertBalanced(LythonRuntime.ExecutionContext context)
    {
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Null(context.CurrentExecutableFrame);
    }

    private sealed class Tap : LythonRuntime.ICallable
    {
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Calls++;
            return arguments[0].Value;
        }
    }
}
