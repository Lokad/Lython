using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FunctionDefaultConstructionTests
{
    [Fact]
    public void DefaultContainerStorageAddsToTheSameParameterLayout()
    {
        var parameters = Enumerable.Range(0, 8)
            .Select(i => new LoweredFunctionParameter("a" + i, FunctionParameterKind.Positional, null)).ToArray();
        var requiredContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var defaultContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        _ = FunctionBindingPlan.CreateDynamic("f", PythonCallableKind.Function, parameters, new Dictionary<string, object>(), requiredContext);
        var defaults = parameters.ToDictionary(p => p.Name, _ => (object)0);
        var plan = FunctionBindingPlan.CreateDynamic("f", PythonCallableKind.Function, parameters, defaults, defaultContext);
        Assert.True(plan.TryGetDefaultMetadata("__defaults__", out var tuple));
        Assert.Equal(parameters.Length, Assert.IsType<PyTuple>(tuple).Count);
        Assert.True(defaultContext.MemoryGovernor.CurrentCommittedBytes > requiredContext.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void DeniedMetadataConstructionRefundsOnlyItsUnpublishedStorage()
    {
        var denials = 0;
        var completed = 0;
        foreach (var available in new[] { 0, 64, 512, 768, 4096 })
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) },regexProvider:Utf8RegexProvider.Instance);
            var governor = context.MemoryGovernor;
            var supplied = new PyList([42], governor, null);
            var padding = 1024 * 1024 - governor.CurrentAccountedBytes - available;
            governor.Reserve(padding, null);
            governor.Commit(padding);
            var before = governor.CurrentCommittedBytes;
            FunctionBindingPlan? plan = null;
            try
            {
                plan = FunctionBindingPlan.CreateDynamic("f", PythonCallableKind.Function,
                    [new("a", FunctionParameterKind.Positional, null), new("b", FunctionParameterKind.KeywordOnly, null)],
                    new Dictionary<string, object> { ["a"] = supplied, ["b"] = supplied }, context);
                Assert.True(plan.TryGetPositionalDefault(0, out var positional));
                Assert.Same(supplied, positional.Object);
                Assert.True(plan.KeywordDefaultStorage!.TryGetValue(plan.KeywordDefaultName(0), out var keyword));
                Assert.Same(supplied, keyword);
                completed++;
            }
            catch (LythonRuntimeException error)
            {
                Assert.Equal("MemoryError", error.ExceptionType);
                Assert.Null(plan);
                Assert.Equal(before, governor.CurrentCommittedBytes);
                Assert.Equal(0, governor.CurrentReservedBytes);
                denials++;
            }
            Assert.Single(supplied);
            Assert.Equal(42, supplied[0]);
        }
        Assert.True(denials > 1);
        Assert.True(completed > 0);
    }

    [Fact]
    public void RefusedLargeDefaultLeavesEarlierAliasedValueAndPrivateStorageFunded()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var owner = budget.CreateScope();
        var gate = new AdmissionHookMemoryBudget(owner);
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = gate });
        var governor = context.MemoryGovernor;
        var baseline = budget.ReservedBytes;
        var span = new LythonSourceSpan(0, 1, 1, 1);
        var tuple = PyTuple.CreateDefaultStorage(2, context, span);
        var alias = new PyList([], governor, span);
        tuple.SetPrivateDefault(0, LythonRuntime.ExecutableSlot.FromObject(alias), context, span);
        var beforeRefusal = budget.ReservedBytes;
        try
        {
            gate.RefuseGrowth = _ => true;
            var large = LythonRuntime.ExecutableSlot.FromObject(BigInteger.One << 127);
            var failure = Assert.Throws<LythonRuntimeException>(() => tuple.SetPrivateDefault(1, large, context, span));
            Assert.Same(governor.AdmissionFailure, failure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, failure.Attribution?.Reason);
            Assert.Equal(beforeRefusal, budget.ReservedBytes);
            Assert.Same(alias, tuple[0]);
        }
        finally { governor.RefundUnpublishedValue(tuple); }
        Assert.Equal(baseline + alias.CommittedStorageBytes, budget.ReservedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
        context.State.CloseResources();
    }
}
