using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeIntegerChainTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PromotedArithmeticPreservesFirstRefusalCancellationAndInputOwnership(bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var gate = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { MemoryBudget = gate, CancellationToken = cancellation.Token });
            var span = new LythonSourceSpan(0, 1, 1, 1);
            try
            {
                LythonRuntime.ExecuteExecutableCodeObject(Compile("def calculate(a,b): return (a*b+5)//3"), context);
                var function = Assert.IsType<PyExecutableFunction>(context.Variables["calculate"]);
                var left = LythonRuntime.OwnFreshInteger(BigInteger.One << 65536, context.MemoryGovernor, span);
                object right = new BigInteger(7);
                var call = Assert.Single(Compile("answer=calculate(left,right)").CallSites);
                var baseline = budget.ReservedBytes;
                if (cancel) gate.AfterGrowth = _ => cancellation.Cancel();
                else gate.RefuseGrowth = _ => true;
                var error = Assert.Throws<LythonRuntimeException>(() => function.StartNativeCall(call,
                    [left, right], null, context, span, false, true, default, out _, out _, out _));
                Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                if (!cancel) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Equal(BigInteger.One << 65536, Assert.IsType<BigInteger>(left));
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static ExecutableCodeObject Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
    }
}
