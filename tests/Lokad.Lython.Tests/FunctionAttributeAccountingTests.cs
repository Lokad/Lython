using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Function custom attributes use their canonical governed dictionary. Retained
/// capacity and aliases remain funded; refused growth cannot publish a value.
/// </summary>
public sealed class FunctionAttributeAccountingTests
{
    [Fact]
    public void DeletionRetainsCapacityAndDeniesReinsertionBeforeMutation()
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) },regexProvider:Utf8RegexProvider.Instance);
        var function = new PyFunction("f", [], [], context, new(), ScopeDirectiveFacts.Empty);
        var value = new PyList([42], context.MemoryGovernor, null);
        var beforeTag = context.MemoryGovernor.CurrentAccountedBytes;
        function.TrySetMember("tag", value);
        var beforeDeletion = context.MemoryGovernor.CurrentAccountedBytes;
        Assert.True(function.TryDeleteMember("tag", null));
        Assert.False(function.TryGetMember("tag", out _));
        Assert.False(function.TryDeleteMember("tag", null));
        // Dictionary entry bookkeeping may be returned; retained backing and
        // the separately owned value must remain funded.
        Assert.InRange(context.MemoryGovernor.CurrentAccountedBytes, beforeTag + 1, beforeDeletion);
        Assert.True(function.TryGetMember("__dict__", out var attributes));
        var dictionary = Assert.IsType<PyDict>(attributes);
        var padding = limit - context.MemoryGovernor.CurrentAccountedBytes;
        context.MemoryGovernor.Reserve(padding, null);
        context.MemoryGovernor.Commit(padding);
        Assert.Equal(limit, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => function.TrySetMember("tag", value)).ExceptionType);
        Assert.Empty(dictionary);
        Assert.Equal(42, Assert.Single(value));
        Assert.Equal(limit, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData("__doc__")]
    [InlineData("__module__")]
    public void DeniedReservedResetDoesNotPublishMetadata(string name)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) },regexProvider:Utf8RegexProvider.Instance);
        var function = new PyFunction("f", [], [], context, new(), ScopeDirectiveFacts.Empty);
        var beforeFound = function.TryGetMember(name, out var before);
        var padding = limit - context.MemoryGovernor.CurrentAccountedBytes;
        context.MemoryGovernor.Reserve(padding, null);
        context.MemoryGovernor.Commit(padding);
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => function.TryDeleteMember(name, null)).ExceptionType);
        Assert.Equal(beforeFound, function.TryGetMember(name, out var after));
        Assert.Same(before, after);
        Assert.Equal(limit, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CustomAttributeOverwriteReusesOwnedDictionaryStorage()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var function = new PyFunction(
            "f",
            [],
            [],
            context,
            new Dictionary<string, object>(StringComparer.Ordinal),
            ScopeDirectiveFacts.Empty);
        Assert.True(function.TrySetMember("a", PyNone.Instance));
        var afterFirst = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(function.TrySetMember("a", PyNone.Instance));
        Assert.Equal(afterFirst, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(function.TrySetMember("b", PyNone.Instance));
        Assert.True(function.TrySetMember("c", PyNone.Instance));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > afterFirst);
        Assert.True(function.TryGetMember("__dict__", out var attributes));
        Assert.Equal(3, Assert.IsType<PyDict>(attributes).Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
