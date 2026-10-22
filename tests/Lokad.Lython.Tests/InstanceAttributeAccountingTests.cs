using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Instance table capacity remains owned after deletion and can be reused;
/// overwrites do not grow storage and owner cleanup returns the allowance.
/// </summary>
public sealed class InstanceAttributeAccountingTests
{
    private static PyType NewType() => new("A", [], new Dictionary<string, object>());

    [Fact]
    public void AttributeDeletionReusesFundedCapacityUntilOwnerCleanup()
    {
        var host = new MockLythonHost();
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var instance = new PyInstance(NewType(), context.MemoryGovernor, span);
        instance.SetAttribute("x", 1);
        instance.SetAttribute("y", 2);
        var charged = context.MemoryGovernor.CurrentCommittedBytes;
        instance.SetAttribute("x", 3);
        Assert.Equal(charged, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(instance.RemoveAttribute("x"));
        Assert.Equal(charged, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.False(instance.RemoveAttribute("missing"));
        instance.SetAttribute("z", 4);
        Assert.Equal(charged, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(instance.TryGetOwnAttribute("z", out var value));
        Assert.Equal(4, value);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void UngovernedAttributesStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var instance = new PyInstance(NewType());
        instance.SetAttribute("x", 1);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
