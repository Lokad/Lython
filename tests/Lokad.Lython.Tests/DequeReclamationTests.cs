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


    [Fact]
    public void DroppedDequeReleasesShellAndNodes()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        CreateTrackedDeque(pool, governor);
        // Shell plus three nodes beside the registry entry.
        Assert.Equal(128L + 3L * 64L + 128L + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        // A partial sweep releases entries but keeps tier capacity: the
        // remainder reconciles explicitly against backing, which abandonment frees.
        Assert.Equal(128L + 3L * 64L + 128L, pool.Sweep());
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    // The deque never escapes its framing helper: in Debug builds a test-scope
    // temporary merged across the collection boundary would pin the entry.
    [Fact]
    public void PoppedNodesResnapshotCoupon()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        PopTrackedDeque(pool, governor);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.Equal(128L + 2L * 64L + 128L, pool.Sweep());
        Assert.Equal(pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void PopTrackedDeque(ChargeReclamationPool pool, MemoryGovernor governor)
    {
        var deque = new PyDeque(null, governor, null);
        deque.Append(new object());
        deque.Append(new object());
        deque.Append(new object());
        pool.TrackFreshMutable(deque, deque.CommittedStorageBytes);
        deque.Pop();
        Assert.Equal(128L + 2L * 64L + 128L + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
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
