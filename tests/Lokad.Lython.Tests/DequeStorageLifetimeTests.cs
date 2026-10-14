using Lokad.Lython.Runtime;
namespace Lokad.Lython.Tests;

public sealed class DequeStorageLifetimeTests
{
    [Fact]
    public void RemovedNodesReleaseTheirStorageWhileTheShellStaysOwned()
    {
        var root = new LythonMemoryBudget(4096);
        using var owner = root.CreateScope();
        var governor = new MemoryGovernor(null, owner);
        var deque = new PyDeque(null, governor, null);
        deque.Append(new object());
        deque.Append(new object());
        deque.Append(new object());
        Assert.Equal(128L + 3L * 128L, root.ReservedBytes);
        deque.Pop();
        Assert.Equal(128L + 2L * 128L, root.ReservedBytes);
        Assert.Equal(2, deque.Count);
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);
    }
    [Fact]
    public void AttachOwnsShellAndExistingNodes()
    {
        var governor = new MemoryGovernor(null);
        var deque = new PyDeque();
        deque.Append(new object());
        deque.Append(new object());
        deque.AttachMemoryGovernor(governor);
        Assert.Equal(128L + 2L * 128L, governor.CurrentCommittedBytes);
        Assert.Equal(deque.CommittedStorageBytes, governor.CurrentCommittedBytes);
    }
}
