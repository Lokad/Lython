using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: instance attribute slots commit 64B per new key and release on
/// deletion, alongside the 160B shell; overwrites and ungoverned instances stay free.
/// </summary>
public sealed class InstanceAttributeAccountingTests
{
    private static PyType NewType() => new("A", [], new Dictionary<string, object>());

    [Fact]
    public void AttributeSlotsCommitAndReleaseExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var instance = new PyInstance(NewType(), context.MemoryGovernor, span);
        instance.SetAttribute("x", 1);
        instance.SetAttribute("y", 2);
        instance.SetAttribute("x", 3);
        Assert.Equal(160L + 2L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.True(instance.RemoveAttribute("x"));
        Assert.Equal(160L + 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.False(instance.RemoveAttribute("missing"));
        Assert.Equal(160L + 64L, context.MemoryGovernor.CurrentCommittedBytes);
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
