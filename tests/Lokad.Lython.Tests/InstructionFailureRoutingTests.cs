using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class InstructionFailureRoutingTests
{
    [Theory]
    [InlineData(false, 0), InlineData(true, 0)]
    [InlineData(false, 1), InlineData(true, 1)]
    [InlineData(false, 2), InlineData(true, 2)]
    [InlineData(false, 0, true), InlineData(true, 0, true)]
    [InlineData(false, 1, true), InlineData(true, 1, true)]
    [InlineData(false, 2, true), InlineData(true, 2, true)]
    public async Task EntryFailureKeepsItsOriginWhileOperationFailureAcquiresPythonContext(bool asynchronous, int failureKind,
        bool insideHandler = false)
    {
        var frontend = LythonFrontend.Compile(insideHandler
            ? "namespace = globals()\ntry:\n raise KeyError('inner')\nexcept KeyError as caught:\n arm()\n effects.append(1)\n"
            : "arm()\neffects.append(1)\n");
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())).EntryPoint;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new()
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var effects = new PyList(0, context.MemoryGovernor, null);
            context.Variables["effects"] = effects;
            var active = new PyException("ValueError", "outer", PyNone.Instance);
            context.State.SetCurrentException(active);
            var origin = new LythonSourceSpan(120, 5, 12, 3);
            LythonRuntimeException? first = null;
            PyException? activeDuringOperation = null;
            context.Variables["arm"] = new Arm(() =>
            {
                activeDuringOperation = context.State.CurrentException;
                if (failureKind == 0) cancellation.Cancel();
                else if (failureKind == 1)
                    first = context.MemoryGovernor.RefuseAdmission("controlled provider refusal", 777, origin);
                else throw first = new LythonRuntimeException("TypeError", "controlled operation failure", origin);
            });
            var baseline = context.MemoryGovernor.CurrentAccountedBytes;
            try
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                    else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                });
                if (failureKind == 0) Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                else
                {
                    Assert.Same(first, error);
                    Assert.Equal(origin, error.Span);
                    if (failureKind == 1)
                    {
                        Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                        Assert.Equal(777L, error.Attribution?.Requested);
                        Assert.Same(first, context.MemoryGovernor.AdmissionFailure);
                    }
                }
                if (failureKind == 2) Assert.Same(activeDuringOperation, error.PythonContext);
                else Assert.Null(error.PythonContext);
                Assert.Same(active, context.State.CurrentException);
                Assert.False(context.Variables.ContainsKey("caught"));
                Assert.Empty(effects);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
            if (insideHandler) Assert.True(context.MemoryGovernor.CurrentAccountedBytes >= baseline);
            else Assert.Equal(baseline, context.MemoryGovernor.CurrentAccountedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private sealed class Arm(Action action) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.Empty(arguments);
            action();
            return PyNone.Instance;
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
            => new(Invoke(arguments, span, context));
    }
}
