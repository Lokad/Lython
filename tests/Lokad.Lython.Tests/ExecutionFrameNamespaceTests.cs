using Lokad.Lython.Runtime;
using Lokad.Lython.Frontend;
using Lokad.Lython.Tests.Harness;
using System.Reflection;

namespace Lokad.Lython.Tests;

public sealed class ExecutionFrameNamespaceTests
{
    [Fact]
    public void LexicalReadsDoNotMaterializeAnUnusedNamespace()
    {
        var parent = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        var child = new LythonRuntime.ExecutionContext(parent, ScopeDirectiveFacts.Empty);
        var span = new LythonSourceSpan(0, 0, 1, 1);
        Assert.True(parent.TryGetBuiltin("int", out var expected));
        Assert.Same(expected, LythonRuntime.ResolveName("int", span, child));
        Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ResolveName("missing", span, child));
        Assert.Null(typeof(ExecutionFrame).GetField("_variables", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(child));
        var actualNamespace = child.Variables;
        actualNamespace["value"] = expected;
        Assert.Same(expected, LythonRuntime.ResolveName("value", span, child));
        Assert.Same(actualNamespace, child.Variables);
    }

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
