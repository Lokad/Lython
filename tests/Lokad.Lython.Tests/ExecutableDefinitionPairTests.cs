using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableDefinitionPairTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosedDefaultsRequireTwoPrivateDefinitionsAndAnExactPairReturn(bool guestCall)
    {
        var value = guestCall ? "read() + 1" : "seed % 5 + 1";
        var syntax = LythonFrontend.Compile($$"""
            def make(seed):
                def first(value, offset=seed % 7, /, *, factor={{value}}, enabled=seed % 3 != 0):
                    return (value + offset) * factor if enabled else value
                other = lambda value, offset=(seed + 1) % 7, /, *, factor=2: (value - offset) * factor
                return first, other
            first, other = make(5)
            """).Script!;
        var module = ExecutableScript.Compile(syntax).EntryPoint;
        var factory = Assert.Single(module.FunctionBindings()).CodeObject!;
        Assert.Equal(!guestCall, factory.HasDirectDefinitionPair);
        Assert.Contains(module.CallSites, call => call.UnpackedReturnCount == 2);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task InterruptedSecondDefaultStoragePrecedesArithmeticAndRefunds(bool asynchronous, bool cancelDuringAdmission)
    {
        const string source = """
            def make(seed):
                def first(value=seed % 7):
                    return value
                other = lambda left=10 // seed, right=seed + 1: left + right
                return first, other
            first, other = make(0)
            """;
        var syntax = LythonFrontend.Compile(source).Script!;
        var module = ExecutableScript.Compile(syntax).EntryPoint;
        Assert.True(Assert.Single(module.FunctionBindings()).CodeObject!.HasDirectDefinitionPair);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var bytes = PyTuple.EstimateDefaultStorage(2);
            if (cancelDuringAdmission)
            {
                hook.AfterGrowth = growth =>
                {
                    if (growth == bytes) cancellation.Cancel();
                };
            }
            else hook.RefuseGrowth = growth => growth == bytes;
            try
            {
                var error = asynchronous
                    ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(module, context))
                    : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(module, context, default, null));
                Assert.Equal(cancelDuringAdmission ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                Assert.Equal(source.IndexOf("lambda", StringComparison.Ordinal), error.Span?.Start);
                if (!cancelDuringAdmission) Assert.Equal(bytes, error.Attribution?.Requested);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}
