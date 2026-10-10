using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class ExecutionFrameNamespaceTests
{
    [Fact]
    public void NamespaceAliasesRemainStableAcrossConcurrentFirstReads()
    {
        var parent = new ExecutionFrame(null, new(StringComparer.Ordinal));
        var child = new ExecutionFrame(parent);
        var aliases = new Dictionary<string, object>[64];
        Parallel.For(0, aliases.Length, i => aliases[i] = child.Variables);
        aliases[0]["sentinel"] = 42;
        Assert.All(aliases, alias =>
        {
            Assert.Same(child.Variables, alias);
            Assert.Equal(42, alias["sentinel"]);
        });
        Assert.Empty(parent.Variables);
    }

    [Fact]
    public void ChildNamespacesRemainIndependentAndOrdinal()
    {
        var parent = new ExecutionFrame(null, new(StringComparer.Ordinal) { ["value"] = 1 });
        var first = new ExecutionFrame(parent);
        var second = new ExecutionFrame(parent);
        first.Variables["value"] = 2;
        first.Variables["Value"] = 3;
        Assert.Equal(2, first.Variables.Count);
        Assert.Equal(1, parent.Variables["value"]);
        Assert.Empty(second.Variables);
    }
}
