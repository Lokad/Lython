namespace Lokad.Lython.Runtime;

// Construction transactions use these local storage estimates to refund only
// fresh unpublished values. Published values remain owned by their batch;
// this interface neither registers identities nor describes graph edges.
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
}
