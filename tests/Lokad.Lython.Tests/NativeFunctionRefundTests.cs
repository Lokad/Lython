using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class NativeFunctionRefundTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedRefundReturnsBoundStorageOnceAndKeepsLiveOwnersFunded(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var governor = context.MemoryGovernor;
            var span = new LythonSourceSpan(17, 4, 2, 5);
            var definition = Assert.IsType<LoweredFunctionDefinitionStatement>(Assert.Single(
                LoweredScript.Lower(LythonFrontend.Compile("def f(value,unused): return value.lower()").Script!).Statements));
            var code = ExecutableScript.CompileClassFunction(definition);
            Assert.True(code.CanInlineInstructions);
            var function = new PyExecutableFunction("f", definition.ParameterLayout, code, context, [],
                new Dictionary<string, object>(), definition.ScopeFacts);
            var retained = PyString.FromString(new string('A', 4096), governor, span);
            var bound = function.BindCallOperands([retained, PyNone.Instance], span, context, receiver: null, callSite: null);
            LythonRuntime.ReturnBoundArguments(bound, code.ValueStorageCapacity);
            LythonRuntimeException? first = null;
            long fundedAtFailure = -1;
            hook.AfterGrowth = _ =>
            {
                // Model legitimate admission reentry while the native helper
                // constructs its output. A smaller returned workspace is idle
                // when the outer call relinquishes its own larger storage.
                hook.AfterGrowth = null;
                var nested = governor.RentFunctionWorkspace(1, span);
                governor.ReturnFunctionWorkspace(nested, 0);
                hook.AfterShrink = _ =>
                {
                    hook.AfterShrink = null;
                    first = Assert.Throws<LythonRuntimeException>(() => governor.Reserve(limit + 1, span));
                    fundedAtFailure = governor.CurrentAccountedBytes;
                    throw first;
                };
            };
            try
            {
                var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    CallArgumentValue[] arguments = [CallArgumentValue.Positional(retained), CallArgumentValue.Positional(PyNone.Instance)];
                    if (asynchronous) await function.InvokeAsync(arguments, span, context);
                    else function.Invoke(arguments, span, context);
                });
                Assert.NotNull(first);
                Assert.Same(first, failure);
                Assert.Same(first, governor.AdmissionFailure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
                Assert.Same(span, failure.Span);
                Assert.Equal(fundedAtFailure, governor.CurrentAccountedBytes);
                Assert.Equal(fundedAtFailure, budget.ReservedBytes);
                Assert.Equal(4096, retained.ByteLength);
                Assert.All(retained.Utf8Span.ToArray(), value => Assert.Equal((byte)'A', value));
                Assert.Equal(0, governor.CurrentReservedBytes);
                Assert.Equal(0, context.State.CurrentInterpreterDepth);
                Assert.Equal(0, context.State.CurrentRecursionDepth);
            }
            finally
            {
                hook.AfterGrowth = null;
                hook.AfterShrink = null;
                context.State.CloseResources();
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
