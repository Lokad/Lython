using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

// M03: deque shells charge once per governed instance and every release path
// re-snapshots the pool coupon, so a dropped deque releases exactly shell
// plus live nodes with no safe-direction residue beyond later growth.
public sealed class DequeReclamationTests
{
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void CreateTrackedDeque(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var deque = new PyDeque(null, governor, null);
        deque.Append(new object());
        deque.Append(new object());
        deque.Append(new object());
        pool.TrackFreshMutable(deque, deque.CommittedStorageBytes);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static PyDeque BuildTrackedDeque(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var deque = new PyDeque(null, governor, null);
        deque.Append(new object());
        deque.Append(new object());
        deque.Append(new object());
        pool.TrackFreshMutable(deque, deque.CommittedStorageBytes);
        return deque;
    }

    [Fact]
    public void DroppedDequeReleasesShellAndNodes()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        CreateTrackedDeque(pool, governor);
        // Shell plus three nodes beside the registry entry.
        Assert.Equal(128L + 3L * 64L + 64L, governor.CurrentCommittedBytes);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(128L + 3L * 64L + 64L, pool.Sweep());
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void PoppedNodesResnapshotCoupon()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        var deque = BuildTrackedDeque(pool, governor);
        deque.Pop();
        Assert.Equal(128L + 2L * 64L + 64L, governor.CurrentCommittedBytes);
        deque = null!;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(128L + 2L * 64L + 64L, pool.Sweep());
        Assert.Equal(0, governor.CurrentCommittedBytes);
    }

    [Fact]
    public void AttachOwnsShellAndExistingNodes()
    {
        // Nodes built while ungoverned charge on first attach, or later drops
        // would strand them beside a shelled coupon.
        var governor = new MemoryGovernor(null);
        var deque = new PyDeque();
        deque.Append(new object());
        deque.Append(new object());
        Assert.Equal(0, governor.CurrentCommittedBytes);
        deque.AttachMemoryGovernor(governor);
        Assert.Equal(128L + 2L * 64L, governor.CurrentCommittedBytes);
        Assert.Equal(128L + 2L * 64L, deque.CommittedStorageBytes);
        GC.KeepAlive(deque);
    }
}
