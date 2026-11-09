using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CompletedHelperCallOwnershipTests
{
    [Theory]
    [InlineData(false, 0, false), InlineData(true, 0, false)]
    [InlineData(false, 0, true), InlineData(true, 0, true)]
    [InlineData(false, 1, false), InlineData(true, 1, false)]
    [InlineData(false, 1, true), InlineData(true, 1, true)]
    [InlineData(false, 2, false), InlineData(true, 2, false)]
    [InlineData(false, 2, true), InlineData(true, 2, true)]
    public async Task CompletedHelpersKeepInputFundingAndUnwindBeforeFollowingEffects(bool asynchronous, int failureKind, bool nested)
    {
        // The two-statement caller also exercises a deferred lexical context.
        var source = """
def lower(value):
    return value.lower()
def through(value):
    result = lower(value)
    return result
effects.append(through(input))
""";
        if (nested) source = "def main():\n" + string.Join('\n', source.Split('\n').Select(line => " " + line)) + "\nmain()\n";
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
                Assert.True(context.State.CurrentInterpreterDepth > 0);
                Assert.True(context.State.CurrentRecursionDepth > 0);
                if (failureKind == 1) cancellation.Cancel();
                if (failureKind == 2)
                    first = context.MemoryGovernor.RefuseAdmission("controlled helper admission refusal", 777, origin);
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
                    Assert.Equal(new string('a', input.Length), Assert.IsType<PyString>(Assert.Single(effects)).AsString());
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
