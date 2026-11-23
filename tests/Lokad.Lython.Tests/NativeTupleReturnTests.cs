using Slot = Lokad.Lython.Runtime.LythonRuntime.ExecutableSlot;
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
                LythonRuntime.ExecutableSlot[] returned = new LythonRuntime.ExecutableSlot[2];
                function.StartNativeCall(call, [Slot.FromObject(left), Slot.FromObject(right)], null, context, Span,
                    false, true, returned, out var unpacked, out _, out var frame);
                Assert.True(unpacked);
                Assert.Null(frame);
                Assert.Same(left, returned[0].Object);
                Assert.Same(right, returned[1].Object);
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void AdmissionFailureOrCancellationLeavesCallerStorageUntouched(bool cancel, bool integers)
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
                var function = Define(context, integers);
                object left = integers ? new System.Numerics.BigInteger(1200) : PyString.FromString("left");
                object right = integers ? new System.Numerics.BigInteger(1400) : PyString.FromString("right");
                var call = Assert.Single(Compile("a,b=pair(left,right)").CallSites);
                var first = new object();
                var second = new object();
                Slot[] returned = [Slot.FromObject(first), Slot.FromObject(second)];
                var baseline = budget.ReservedBytes;
                if (cancel) gate.AfterGrowth = _ => cancellation.Cancel();
                else gate.RefuseGrowth = _ => true;
                var error = Assert.Throws<LythonRuntimeException>(() => function.StartNativeCall(call,
                    [Slot.FromObject(left), Slot.FromObject(right)], null, context, Span, false, true, returned, out _, out _, out _));
                Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                if (!cancel) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Same(first, returned[0].Object);
                Assert.Same(second, returned[1].Object);
                Assert.Equal(baseline, budget.ReservedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static PyExecutableFunction Define(LythonRuntime.ExecutionContext context, bool integers = false)
    {
        var source = integers
            ? "def pair(left, right):\n    first = left + 1\n    second = right + 1\n    return first, second"
            : "def pair(left,right): return left,right";
        LythonRuntime.ExecuteExecutableCodeObject(Compile(source), context);
        return Assert.IsType<PyExecutableFunction>(context.Variables["pair"]);
    }

    private static ExecutableCodeObject Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        return ExecutableScript.Compile(frontend.Script!).EntryPoint;
    }
}
