using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeTupleReturnTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Fact]
    public void ReturnedElementsKeepAliasesAndConservativeFunding()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            try
            {
                var function = Define(context);
                var left = PyString.FromString(new string('x', 8192), context.MemoryGovernor, Span);
                var right = new PyList(0, context.MemoryGovernor, Span);
                var baseline = budget.ReservedBytes;
                var call = Assert.Single(Compile("a,b=pair(left,right)").CallSites);
                object[] returned = new object[2];
                function.StartNativeCall(call, [left, right], null, context, Span,
                    false, true, returned, out var unpacked, out _, out var frame);
                Assert.True(unpacked);
                Assert.Null(frame);
                Assert.Same(left, returned[0]);
                Assert.Same(right, returned[1]);
                Assert.True(budget.ReservedBytes > baseline);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdmissionFailureOrCancellationLeavesCallerStorageUntouched(bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var gate = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { MemoryBudget = gate, CancellationToken = cancellation.Token });
            try
            {
                var function = Define(context);
                var left = PyString.FromString("left");
                var right = PyString.FromString("right");
                var call = Assert.Single(Compile("a,b=pair(left,right)").CallSites);
                var first = new object();
                var second = new object();
                object[] returned = [first, second];
                var baseline = budget.ReservedBytes;
                if (cancel) gate.AfterGrowth = _ => cancellation.Cancel();
                else gate.RefuseGrowth = _ => true;
                var error = Assert.Throws<LythonRuntimeException>(() => function.StartNativeCall(call,
                    [left, right], null, context, Span, false, true, returned, out _, out _, out _));
                Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                if (!cancel) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Same(first, returned[0]);
                Assert.Same(second, returned[1]);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static PyExecutableFunction Define(LythonRuntime.ExecutionContext context)
    {
        LythonRuntime.ExecuteExecutableCodeObject(Compile("def pair(left,right): return left,right"), context);
        return Assert.IsType<PyExecutableFunction>(context.Variables["pair"]);
    }

    private static ExecutableCodeObject Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!)).EntryPoint;
    }
}
