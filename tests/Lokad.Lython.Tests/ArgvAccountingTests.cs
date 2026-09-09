using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG22: argv string payload (128 bytes base plus UTF-8 length) and backing
/// array slots (32 plus 16 per entry) commit at construction; empty argv stays
/// free.
/// </summary>
public sealed class ArgvAccountingTests
{
    [Fact]
    public void ArgvPayloadAndArrayCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(
            host,
            new LythonRunOptions { Args = ["ab", "cde"] });
        // Array: 32 + 16 * 2 = 64; strings: (128 + 2) + (128 + 3) = 261.
        Assert.Equal(64L + 261L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(2, context.State.Args.Count);
    }

    [Fact]
    public void EmptyArgvStaysFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}