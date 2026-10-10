using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Lokad.Lython.Frontend;

namespace Lokad.Lython.Tests;

public sealed class ExecutionContextArchitectureSubsystemTests
{
    [Fact]
    public void ChildExecutionContext_SharesServicesButGetsDistinctFrame()
    {
        var parent = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
        var child = new LythonRuntime.ExecutionContext(parent);

        Assert.Same(parent.Services, child.Services);
        Assert.NotSame(parent.Frame, child.Frame);
        Assert.Same(parent.Frame, child.Frame.Parent);
        Assert.Same(parent, child.ParentContext);
    }

    [Fact]
    public void FunctionNamespaceAliasesRemainStableAcrossConcurrentContextAndFrameReads()
    {
        var parent = new LythonRuntime.ExecutionContext(new MockLythonHost(), options: null);
        var child = new LythonRuntime.ExecutionContext(parent, ScopeDirectiveFacts.Empty);
        var aliases = new Dictionary<string, object>[64];
        Parallel.For(0, aliases.Length, i =>
            aliases[i] = i % 2 == 0 ? child.Variables : child.Frame.Variables);

        aliases[0]["sentinel"] = 42;
        aliases[0]["Sentinel"] = 43;
        Assert.All(aliases, alias =>
        {
            Assert.Same(child.Variables, alias);
            Assert.Equal(42, alias["sentinel"]);
            Assert.Equal(43, alias["Sentinel"]);
        });
        Assert.Same(child.Variables, child.Frame.Variables);
        Assert.False(parent.Variables.ContainsKey("sentinel"));
        Assert.Same(parent.Frame, child.Frame.Parent);
    }
}

