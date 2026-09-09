using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG22: environment table copies commit 80 bytes plus 32 per entry at
/// construction; absent or empty environments stay free.
/// </summary>
public sealed class EnvironmentAccountingTests
{
    [Fact]
    public void EnvironmentTableCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(
            host,
            new LythonRunOptions { Environment = new Dictionary<string, string> { ["a"] = "b", ["c"] = "d" } });
        Assert.Equal(80L + (32L * 2), context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(2, context.State.Environment.Count);
    }

    [Fact]
    public void EmptyEnvironmentStaysFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}