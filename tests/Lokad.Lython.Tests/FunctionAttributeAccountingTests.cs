using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: function metadata tables charge per new key like instance attribute
/// tables; overwrites stay free. Functions always carry their closure
/// governor, so there is no ungoverned case.
/// </summary>
public sealed class FunctionAttributeAccountingTests
{
    [Fact]
    public void DeletionRetainsCapacityAndDeniesReinsertionBeforeMutation()
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        var function = new PyFunction("f", [], [], context, new(), ScopeDirectiveFacts.Empty);
        var value = new PyList([42], context.MemoryGovernor, null);
        function.TrySetMember("tag", value);
        var padding = limit - context.MemoryGovernor.CurrentAccountedBytes;
        context.MemoryGovernor.Reserve(padding, null);
        context.MemoryGovernor.Commit(padding);
        Assert.True(function.TryDeleteMember("tag", null));
        Assert.False(function.TryGetMember("tag", out _));
        Assert.False(function.TryDeleteMember("tag", null));
        Assert.Equal(limit, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => function.TrySetMember("tag", value)).ExceptionType);
        Assert.False(function.TryGetMember("tag", out _));
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
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
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
    public void MetadataSlotsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var function = new PyFunction(
            "f",
            [],
            [],
            context,
            new Dictionary<string, object>(StringComparer.Ordinal),
            ScopeDirectiveFacts.Empty);
        Assert.True(function.TrySetMember("a", PyNone.Instance));
        Assert.True(function.TrySetMember("a", PyNone.Instance));
        Assert.True(function.TrySetMember("b", PyNone.Instance));
        Assert.True(function.TrySetMember("c", PyNone.Instance));
        // Three attribute slots beside the owned definition name (128 + 1).
        Assert.Equal(3L * 64L + 129L,
            context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
