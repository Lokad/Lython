using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG10: deque nodes pay per live node; eviction reuses the charge and clear
/// releases exactly what is held. Empty deques stay free like empty sets.
/// </summary>
public sealed class DequeAccountingTests
{
    [Fact]
    public void AppendsAccumulateAndClearReleases()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var deque = new PyDeque(null, context.MemoryGovernor, span);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        for (var i = 0; i < 100; i++)
        {
            deque.Append(new BigInteger(i));
        }

        Assert.Equal(100L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        deque.Clear();
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void BoundedEvictionReusesNodeCharge()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var deque = new PyDeque(8, context.MemoryGovernor, span);
        for (var i = 0; i < 20; i++)
        {
            deque.Append(new BigInteger(i));
        }

        Assert.Equal(8, deque.Count);
        Assert.Equal(8L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void PopAndRemoveReleaseNodeCharges()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var deque = new PyDeque(null, context.MemoryGovernor, span);
        for (var i = 0; i < 10; i++)
        {
            deque.Append(new BigInteger(i));
        }

        _ = deque.Pop();
        _ = deque.PopLeft();
        Assert.Equal(8L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(deque.RemoveValue(new BigInteger(5)));
        Assert.Equal(7L * 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}