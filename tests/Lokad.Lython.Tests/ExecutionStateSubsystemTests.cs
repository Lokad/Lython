using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutionStateSubsystemTests
{
    [Fact]
    public void ExecutionState_OwnsRunLocalCachesAndBuiltinInventory()
    {
        var host = new MockLythonHost();
        var state = new ExecutionState(host, options: null);

        Assert.Same(host, state.Host);
        Assert.Empty(state.ImportedModules);
        Assert.Empty(state.LoadingModules);
        Assert.Contains("str", ExecutionState.BuiltinNames);
        Assert.Contains("repr", ExecutionState.BuiltinNames);
        Assert.Contains("sum", ExecutionState.BuiltinNames);
        Assert.Contains("bytes", ExecutionState.BuiltinNames);
        Assert.Contains("TypeError", ExecutionState.BuiltinNames);
    }
}
