using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ComparisonBranchOwnershipTests
{
    [Theory]
    [InlineData(false, 0), InlineData(true, 0)]
    [InlineData(false, 1), InlineData(true, 1)]
    [InlineData(false, 2), InlineData(true, 2)]
    public async Task ComparisonAndTruthPreserveFirstFailureAndInputFunding(bool asynchronous, int failureKind)
    {
        foreach (var inTruth in new[] { false, true })
        {
            var source = "class Verdict:\n def __bool__(self):\n" +
                (inTruth ? "  effects.append(input.lower())\n" : "") + "  return True\n" +
                "class Left:\n def __lt__(self,other):\n" +
                (!inTruth ? "  effects.append(input.lower())\n" : "") + "  return Verdict()\n" +
                "try:\n if Left()<0:effects.append('branch')\nexcept MemoryError:\n effects.append('suppressed')\nfinally:\n pass\n";
            var frontend = LythonFrontend.Compile(source);
            Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
            var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())).EntryPoint;
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var cancellation = new CancellationTokenSource();
            using (var owner = budget.CreateScope())
            {
                var hook = new AdmissionHookMemoryBudget(owner);
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = hook, CancellationToken = cancellation.Token });
                var origin = new LythonSourceSpan(120, 5, 12, 3);
                var input = PyString.FromString(new string('A', 20000), context.MemoryGovernor, origin);
                var effects = new PyList(0, context.MemoryGovernor, origin);
                context.Variables["input"] = input;
                context.Variables["effects"] = effects;
                var fundedInput = budget.ReservedBytes;
                var reached = false;
                LythonRuntimeException? first = null;
                hook.AfterGrowth = bytes =>
                {
                    if (bytes < input.Utf8Bytes.Length) return;
                    hook.AfterGrowth = null;
                    reached = true;
                    Assert.True(budget.ReservedBytes >= fundedInput + input.Utf8Bytes.Length);
                    if (failureKind == 1) cancellation.Cancel();
                    if (failureKind == 2)
                        first = context.MemoryGovernor.RefuseAdmission("controlled comparison refusal", 777, origin);
                };
                try
                {
                    async Task Execute()
                    {
                        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                    }
                    if (failureKind == 0)
                    {
                        await Execute();
                        Assert.Equal(2, effects.Count);
                        Assert.Equal(new string('a', 20000), Assert.IsType<PyString>(effects[0]).AsString());
                        Assert.Equal("branch", Assert.IsType<PyString>(effects[1]).AsString());
                    }
                    else
                    {
                        var error = await Assert.ThrowsAsync<LythonRuntimeException>(Execute);
                        Assert.Empty(effects);
                        if (failureKind == 1) Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                        else
                        {
                            Assert.Same(first, error);
                            Assert.Same(first, context.MemoryGovernor.AdmissionFailure);
                            Assert.Equal(origin, error.Span);
                            Assert.Equal(777L, error.Attribution?.Requested);
                        }
                    }
                    Assert.True(reached);
                    Assert.Equal(new string('A', 20000), input.AsString());
                    Assert.True(budget.ReservedBytes >= fundedInput);
                    Assert.Null(context.CurrentExecutableFrame);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Equal(0, context.State.CurrentInterpreterDepth);
                    Assert.Equal(0, context.State.CurrentRecursionDepth);
                }
                finally { context.State.CloseResources(); }
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}
