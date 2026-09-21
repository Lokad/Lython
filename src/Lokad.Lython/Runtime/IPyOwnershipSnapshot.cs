namespace Lokad.Lython.Runtime;

// R16: the common ownership snapshot for pool registration. Each governed
// value reports the exact committed bytes the reclamation pool must retain
// while it is reachable, so snapshot rules live on the values instead of a
// pool-side concrete-type switch. Unowned values (shared empties, values
// built outside any governor) report no ownership. Transfer state stays where
// it belongs: callers choose the refunding TrackFresh* twins for fresh
// orphanable results and the plain Track*/TrackCallResult paths for aliases.
internal interface IPyOwnershipSnapshot
{
    bool TrySnapshotOwnership(out long chargeBytes);
}

internal static class OwnershipSnapshot
{
    // Shared owned/unowned shape: governed values snapshot their committed
    // amount, unowned values carry nothing to release.
    internal static bool Owned(MemoryGovernor? governor, long committedBytes, out long chargeBytes)
    {
        if (governor is null)
        {
            chargeBytes = 0;
            return false;
        }

        chargeBytes = committedBytes;
        return true;
    }

    // Fixed shell charge for dict view wrappers, which own no backing to
    // snapshot; the coupon is exact and immutable.
    internal const long ViewShellBytes = 64L;
}
