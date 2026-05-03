using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

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
        Assert.Same(parent, child.Parent);
    }
}

