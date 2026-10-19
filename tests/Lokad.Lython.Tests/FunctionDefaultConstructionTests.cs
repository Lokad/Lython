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
            .Select(i => new LoweredFunctionParameter("a" + i, FunctionParameterKind.Positional, null, null)).ToArray();
        var requiredContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var defaultContext = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        _ = new FunctionBindingPlan("f", PythonCallableKind.Function, parameters, new Dictionary<string, object>(), requiredContext);
        var defaults = parameters.ToDictionary(p => p.Name, _ => (object)0);
        var plan = new FunctionBindingPlan("f", PythonCallableKind.Function, parameters, defaults, defaultContext);
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
                new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(1024 * 1024) });
            var governor = context.MemoryGovernor;
            var supplied = new PyList([42], governor, null);
            var padding = 1024 * 1024 - governor.CurrentAccountedBytes - available;
            governor.Reserve(padding, null);
            governor.Commit(padding);
            var before = governor.CurrentCommittedBytes;
            FunctionBindingPlan? plan = null;
            try
            {
                plan = new FunctionBindingPlan("f", PythonCallableKind.Function,
                    [new("a", FunctionParameterKind.Positional, null, null), new("b", FunctionParameterKind.KeywordOnly, null, null)],
                    new Dictionary<string, object> { ["a"] = supplied, ["b"] = supplied }, context);
                Assert.True(plan.TryGetPositionalDefault(0, out var positional));
                Assert.Same(supplied, positional);
                Assert.True(plan.KeywordDefaultStorage!.TryGetValue(plan.KeywordDefaultNames[0], out var keyword));
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
}
