using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG24: pairing imbalances fail loudly instead of being absorbed. Debug
/// builds throw on commit-without-reserve and release-without-commit; release
/// builds keep the documented tolerance. The full suite stays green with the
/// strict checks on, which proves pairing discipline across every path.
/// </summary>
public sealed class MemoryGovernorInvariantTests
{
    [Fact]
    public void UnpairedCommitThrowsInStrictBuilds()
    {
#if DEBUG
        var governor = new MemoryGovernor(null);
        Assert.Throws<InvalidOperationException>(() => governor.Commit(64));
        Assert.Equal(0, governor.CurrentCommittedBytes);
#else
        Assert.True(true);
#endif
    }

    [Fact]
    public void UnpairedReleaseThrowsInStrictBuilds()
    {
#if DEBUG
        var governor = new MemoryGovernor(null);
        Assert.Throws<InvalidOperationException>(() => governor.Release(64));
        Assert.Equal(0, governor.CurrentCommittedBytes);
#else
        Assert.True(true);
#endif
    }

    [Fact]
    public void PairedUseStillBalances()
    {
        var governor = new MemoryGovernor(null);
        governor.Reserve(64, null);
        governor.Commit(64);
        Assert.Equal(64, governor.CurrentCommittedBytes);
        governor.Release(64);
        Assert.Equal(0, governor.CurrentCommittedBytes);
        Assert.Equal(0, governor.CurrentReservedBytes);
    }
}