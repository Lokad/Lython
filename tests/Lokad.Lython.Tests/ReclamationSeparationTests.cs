using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

// R20: live retention separates cleanly from delayed collection with no
// stranded charges. After promoting a full young tier to old, dropping most
// roots, collecting, and sweeping fully, the released bytes equal exactly the
// dropped coupons and the remaining commitment equals exactly the live
// coupons plus peak tier backing (which never trims by design).
public sealed class ReclamationSeparationTests
{
    [Fact]
    public void DroppedOldTierEntriesReleaseExactlyWithNothingStranded()
    {
        var governor = new MemoryGovernor(null);
        var pool = new ChargeReclamationPool(governor);
        // Every key stays rooted through promotion: ambient collections from
        // parallel suites must not collect anything before the explicit drop
        // below, or the promote sweep would legitimately prune it.
        var all = new List<object>();
        var roots = new List<object>();
        for (var i = 0; i < 6000; i++)
        {
            var key = new object();
            governor.Reserve(128, null);
            governor.Commit(128);
            pool.Track(key, 128);
            all.Add(key);
            if (i % 6 == 5)
            {
                roots.Add(key);
            }
        }

        Assert.Equal(6000, pool.Count);
        pool.Sweep();
        Assert.Equal(6000, pool.Count);

        // Keep every sixth root from the second half: 500 scattered live
        // entries across the old tier, 5500 dropped.
        roots.RemoveRange(0, 500);
        all.Clear();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var released = pool.Sweep(full: true);

        Assert.Equal(5500 * (128 + ChargeReclamationPool.EntryChargeBytes), released);
        Assert.Equal(500, pool.Count);
        var liveExpected = 500L * (128 + ChargeReclamationPool.EntryChargeBytes);
        Assert.Equal(liveExpected + pool.CommittedBackingBytes, governor.CurrentCommittedBytes);
        GC.KeepAlive(roots);
    }
}
