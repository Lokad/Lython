using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

// Set shell funding lasts until batch cleanup. Clear preserves usable Python
// storage without relying on weak registrations or collector sweeps.
[Collection("AllocationSensitive")]
public sealed class SetStorageAccountingTests
{
    [Fact]
    public void UnreferencedSetRemainsBatchOwnedUntilCleanup()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(null, owner);
        var snapshot = TrackDroppedSet(governor,clear: false);
        Assert.True(snapshot > 128L);
        Assert.True(budget.ReservedBytes > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ClearedSetKeepsItsShellUntilBatchCleanup()
    {
        var budget = new LythonMemoryBudget(4096);
        using var owner = budget.CreateScope();
        var governor = new MemoryGovernor(null, owner);
        var snapshot = TrackDroppedSet(governor,clear: true);
        Assert.True(snapshot > 128L);
        Assert.True(budget.ReservedBytes > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long TrackDroppedSet(MemoryGovernor governor,bool clear)
    {
        var set = new PySet(governor, null);
        set.Add(new BigInteger(1));
        set.Add(new BigInteger(2));
        set.Add(new BigInteger(3));
        var snapshot = set.CommittedStorageBytes;
        if (clear)
        {
            set.Clear();
        }

        return snapshot;
    }
}
