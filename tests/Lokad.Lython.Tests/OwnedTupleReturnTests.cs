using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class OwnedTupleReturnTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);
    private const string Definition = """
        def pair(left,right):
            a0=a1=a2=a3=a4=a5=a6=a7=left
            b0=b1=b2=b3=b4=b5=b6=b7=right
            return a7,b7
        """;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OwnedResultsKeepAliasesAndCleanupReturnsAllowance(int failure)
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
                LythonRuntime.ExecuteExecutableCodeObject(Compile(Definition), context);
                var function = Assert.IsType<PyExecutableFunction>(context.Variables["pair"]);
                Assert.False(function.CanStartInline);
                var left = PyString.FromString(new string('x', 8192), context.MemoryGovernor, Span);
                var right = new PyList(0, context.MemoryGovernor, Span);
                var call = Assert.Single(Compile("a,b=pair(left,right)").CallSites);
                var bound = function.BindCallOperands([left, right], Span, context, null, call);
                try
                {
                    var first = new object();
                    var second = new object();
                    object[] returned = [first, second];
                    var baseline = budget.ReservedBytes;
                    if (failure == 1) gate.AfterGrowth = _ => cancellation.Cancel();
                    if (failure == 2) gate.RefuseGrowth = _ => true;
                    if (failure != 0)
                    {
                        var error = Assert.Throws<LythonRuntimeException>(() => function.TryStartBoundInvocation(
                            bound, Span, false, returned, out _, out _, out _));
                        Assert.Equal(failure == 1 ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                            error.Attribution?.Reason);
                        if (failure == 2) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                        Assert.Same(first, returned[0]);
                        Assert.Same(second, returned[1]);
                        Assert.Equal(baseline, budget.ReservedBytes);
                    }
                    else
                    {
                        Assert.True(function.TryStartBoundInvocation(bound, Span, false, returned,
                            out var unpacked, out _, out var frame));
                        Assert.True(unpacked);
                        Assert.Null(frame);
                        Assert.Same(left, returned[0]);
                        Assert.Same(right, returned[1]);
                        Assert.True(budget.ReservedBytes > baseline);
                    }
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Equal(0, context.State.CurrentInterpreterDepth);
                    Assert.Equal(0, context.State.CurrentRecursionDepth);
                }
                finally { LythonRuntime.ReturnBoundArguments(bound, function.CodeObject.ValueStorageCapacity); }
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
