using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableDefinitionPairTests
{
    [Theory]
    [InlineData("", "alternate()", true)]
    [InlineData("    snapshot = locals()\n", "snapshot['alternate']()", false)]
    [InlineData("    seed = seed + 1\n", "alternate()", false)]
    public void ImmediateInvocationRequiresAnUnobservableBinding(string additional, string right, bool prepared)
    {
        var source = "def make(seed):\n" +
            "    offset = seed % 7\n" +
            "    def left(value):\n        return value + offset\n" +
            "    def alternate():\n        step = (seed + 1) % 7\n        return lambda value: value - step\n" +
            additional + "    return left, " + right + "\n" +
            "left, right = make(5)\n";
        var syntax = LythonFrontend.Compile(source).Script!;
        var module = ExecutableScript.Compile(syntax).EntryPoint;
        var factory = Assert.Single(module.FunctionBindings()).CodeObject!;
        Assert.Equal(prepared, factory.Instructions.Any(instruction =>
            instruction.OpCode == ExecutableOpCode.ApplyOperation && instruction.ImmediatelyCalledDefinitionCapture != int.MaxValue));
        Assert.Equal(prepared, factory.HasDirectCapturedDefinitionPair);
        Assert.Equal(prepared, factory.HasDirectDefinitionPair);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task PrivateCallStopsDuringItsRetainedLambdaAdmission(bool asynchronous, bool cancelDuringAdmission, bool capturedPair)
    {
        var source = (capturedPair ? """
            def make(seed):
                offset = seed % 7
                weight = seed % 5 + 1
                enabled = seed % 3 != 0
                def first():
                    return offset * weight if enabled else 0
                def alternate():
                    step = seed + 1
                    return lambda: step
                return first, alternate()
            first, second = make(5)
            """ : """
            def make(seed):
                def first():
                    return 17
                def alternate():
                    step = seed + 1
                    return lambda: step
                return first, alternate()
            first, second = make(5)
            """).ReplaceLineEndings("\n");
        var syntax = LythonFrontend.Compile(source).Script!;
        var module = ExecutableScript.Compile(syntax).EntryPoint;
        var factory = Assert.Single(module.FunctionBindings()).CodeObject!;
        Assert.Equal(capturedPair, factory.HasDirectCapturedDefinitionPair);
        Assert.Equal(capturedPair, factory.HasDirectDefinitionPair);
        Assert.Contains(factory.Instructions,
            instruction => instruction.OpCode == ExecutableOpCode.ApplyOperation &&
                instruction.ImmediatelyCalledDefinitionCapture != int.MaxValue);

        var budget = new LythonMemoryBudget(1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = hook, CancellationToken = cancellation.Token });
            const long bytes = 160 + LythonRuntime.ExecutableCell.StorageBytes;
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
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

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
        var source = """
            def make(seed):
                def first(value=seed % 7):
                    return value
                other = lambda left=10 // seed, right=seed + 1: left + right
                return first, other
            first, other = make(0)
            """.ReplaceLineEndings("\n");
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

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task SecondCallableAdmissionFollowsItsDefaultArithmetic(bool asynchronous, int seed)
    {
        var source = $$"""
            def make(seed):
                def first(value=seed % 7):
                    return value
                other = lambda left=10 // seed, right=seed + 1: left + right
                return first, other
            first, other = make({{seed}})
            """.ReplaceLineEndings("\n");
        var module = ExecutableScript.Compile(LythonFrontend.Compile(source).Script!).EntryPoint;
        Assert.True(Assert.Single(module.FunctionBindings()).CodeObject!.HasDirectDefinitionPair);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
            var defaultBytes = PyTuple.EstimateDefaultStorage(2);
            var admittedDefaults = false;
            hook.AfterGrowth = growth => admittedDefaults |= growth == defaultBytes;
            hook.RefuseGrowth = growth => admittedDefaults && growth == 160;
            try
            {
                var error = asynchronous
                    ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(module, context))
                    : Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(module, context, default, null));
                Assert.True(admittedDefaults);
                if (seed == 0)
                {
                    Assert.Equal("ZeroDivisionError", error.ExceptionType);
                    Assert.Null(context.MemoryGovernor.AdmissionFailure);
                }
                else
                {
                    Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
                    Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                    Assert.Equal(source.IndexOf("lambda", StringComparison.Ordinal), error.Span?.Start);
                }
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
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
    public void ArithmeticFailureRefundsUnpublishedPairWhileItsScopeIsAlive(bool failFirst, bool keywordOnly)
    {
        var firstDefault = failFirst ? "10 // seed" : "seed % 7";
        var keywordMarker = keywordOnly ? "*, " : "";
        var module = ExecutableScript.Compile(LythonFrontend.Compile($$"""
            def make(seed):
                def first({{keywordMarker}}value={{firstDefault}}):
                    return value
                other = lambda {{keywordMarker}}left=10 // seed, right=seed + 1: left + right
                return first, other
            first, other = make(0)
            """).Script!).EntryPoint;
        var factory = Assert.Single(module.FunctionBindings()).CodeObject!;
        Assert.True(factory.HasDirectDefinitionPair);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = owner });
        // The published factory already owns its module namespace in a normal call.
        LythonRuntime.ChargeClosureRetention(context, context.MemoryGovernor, null);
        var before = budget.ReservedBytes;
        var results = new LythonRuntime.ExecutableSlot[2];
        try
        {
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.CompleteClosedDefinitionPair(factory, context, 0, results));
            Assert.Equal("ZeroDivisionError", error.ExceptionType);
            Assert.All(results, result => Assert.True(result.IsUnassigned));
            Assert.Equal(before, budget.ReservedBytes);
        }
        finally { context.State.CloseResources(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CapturedPairFailureRefundsUnpublishedFunctionsAndCells(bool multiple, bool refuseLeftCallable)
    {
        var extra = multiple ? "    factor = seed % 5 + 1\n" : "";
        var calculation = multiple ? "(value + offset) * factor" : "value + offset";
        var source = "def make(seed):\n    offset = seed % 7\n" + extra +
            "    def left(value):\n        return " + calculation + "\n" +
            "    def alternate():\n        step = 7 // seed\n        return lambda value: value - step\n" +
            "    return left, alternate()\nfirst, other = make(0)\n";
        var module = ExecutableScript.Compile(LythonFrontend.Compile(source).Script!).EntryPoint;
        var factory = Assert.Single(module.FunctionBindings()).CodeObject!;
        Assert.True(factory.HasDirectCapturedDefinitionPair);
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var hook = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = hook });
        LythonRuntime.ChargeClosureRetention(context, context.MemoryGovernor, null);
        var before = budget.ReservedBytes;
        var results = new LythonRuntime.ExecutableSlot[2];

        try
        {
            if (refuseLeftCallable) hook.RefuseGrowth = bytes => bytes == 160;
            var error = Assert.Throws<LythonRuntimeException>(() =>
                LythonRuntime.CompleteClosedCapturedPair(factory, context, 0, results));
            Assert.Equal(refuseLeftCallable ? "MemoryError" : "ZeroDivisionError", error.ExceptionType);
            if (refuseLeftCallable) Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
            Assert.All(results, result => Assert.True(result.IsUnassigned));
            Assert.Equal(before, budget.ReservedBytes);
        }
        finally { context.State.CloseResources(); }
    }
}
