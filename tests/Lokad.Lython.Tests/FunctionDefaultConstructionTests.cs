using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FunctionDefaultConstructionTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);

    [Fact]
    public void DefaultContainerStorageAddsToTheSameParameterLayout()
    {
        var parameters = Enumerable.Range(0, 8)
            .Select(i => new GeneratedFunctionParameter("a" + i, FunctionParameterKind.Positional)).ToArray();
        var requiredContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var defaultContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        _ = LythonRuntime.CreateGeneratedBindingPlan("f", parameters, requiredContext, Span);
        var defaults = parameters.Select(parameter => parameter with { DefaultValue = 0 }).ToArray();
        var plan = LythonRuntime.CreateGeneratedBindingPlan("f", defaults, defaultContext, Span);
        Assert.True(plan.TryGetDefaultMetadata("__defaults__", defaultContext, out var tuple));
        Assert.Equal(parameters.Length, Assert.IsType<PyTuple>(tuple).Count);
        Assert.True(defaultContext.MemoryGovernor.CurrentCommittedBytes > requiredContext.MemoryGovernor.CurrentCommittedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacingKeywordDefaultsRefundsOnlyUnobservedStorage(bool observe)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var layout = FunctionParameterLayout.Create(
                [new("first", FunctionParameterKind.KeywordOnly, 1),
                 new("required", FunctionParameterKind.KeywordOnly),
                 new("last", FunctionParameterKind.KeywordOnly, 3)]);
            layout.PrepareKeywordDefaultKeys(default, Span);
            var before = budget.ReservedBytes;
            var values = layout.PreparedKeywordDefaultKeys!.CreatePreparedValues(context, Span);
            object first = new BigInteger(1);
            object last = new BigInteger(3);
            values[0] = first;
            values[1] = last;
            var plan = new FunctionBindingPlan("f", PythonCallableKind.Function, layout, new(null, values));
            var admitted = budget.ReservedBytes;
            Assert.True(admitted > before);
            LythonRuntime.ExecutableSlot[] bound = [default, LythonRuntime.ExecutableSlot.FromInteger(2), default];
            plan.FillPreparedKeywordDefaults(values, bound, context, Span);
            Assert.Same(first, bound[0].Object);
            Assert.Same(last, bound[2].Object);

            PyDict? alias = null;
            if (observe)
            {
                Assert.True(plan.TryGetDefaultMetadata("__kwdefaults__", context, out var metadata));
                alias = Assert.IsType<PyDict>(metadata);
                Assert.Equal(admitted, budget.ReservedBytes);
            }
            Assert.True(plan.TrySetDefaultMetadata("__kwdefaults__", PyNone.Instance, context.MemoryGovernor));
            Assert.Null(plan.PreparedKeywordDefaults);
            Assert.Null(plan.KeywordDefaultStorage);
            Assert.Equal(observe ? admitted : before, budget.ReservedBytes);
            if (alias is not null) Assert.Same(first, alias.GetItem(layout.KeywordDefaultName(0)));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DeniedMetadataConstructionRefundsOnlyItsUnpublishedStorage()
    {
        var denials = 0;
        var completed = 0;
        foreach (var available in new[] { 0, 64, 768, 900, 1024, 4096 })
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
                plan = LythonRuntime.CreateGeneratedBindingPlan("f",
                    [new("a", FunctionParameterKind.Positional, supplied), new("b", FunctionParameterKind.KeywordOnly, supplied)],
                    context, Span);
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
