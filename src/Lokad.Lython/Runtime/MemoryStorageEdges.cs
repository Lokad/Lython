namespace Lokad.Lython.Runtime;

// A cold, stored-field contract. Implementations may neither allocate nor run
// guest getters, hash/equality, iteration, rendering or host operations. Accounting
// and capability back-pointers are not guest-storage children. This is not a
// reclamation contract: visiting a value authorizes no release or refund.
internal interface IMemoryStorageEdges
{
    void VisitStorageChildren(MemoryStorageWalk visitor);
}
