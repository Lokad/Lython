using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: guest-constructed typing.NamedTuple instances commit backing storage
/// at the tuple slot rate (32 plus 16 per field).
/// </summary>
public sealed class TypingNamedTupleAccountingTests
{
    [Fact]
    public void GovernedInstanceCommitsBackingExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var instance = new PyTypingNamedTupleObject(
            "P",
            ["x", "y"],
            [new BigInteger(1), new BigInteger(2)],
            context.MemoryGovernor,
            span);
        Assert.Equal(2, instance.Count);
        Assert.Equal(32L + (16L * 2), context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void UngovernedInstanceStaysFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        _ = new PyTypingNamedTupleObject("P", ["x", "y"], [new BigInteger(1), new BigInteger(2)]);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}