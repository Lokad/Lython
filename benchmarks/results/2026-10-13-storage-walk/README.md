# Funded stored-child traversal prerequisite

Implementation: `5130d9ef116ae58a4e7ae0098e2bc6b818549d89`, production tree
`7250d0eb04694cea1fe59da99b818ee81f38b72c`. This is dormant ownership
infrastructure. It does **not** replace weak reclamation, remove forced GC,
refund guest storage, or establish complete execution-root coverage. No timing
comparison or host-latency qualification was performed.

The [existing inventory](../2026-10-13-ownership-inventory/README.md) identified
stored guest references in containers, wrappers, captures, native state and
resources. The new internal `IMemoryStorageEdges` contract describes audited
stored fields without guest getters, iteration, hashing, equality, rendering,
projection or host operations. Concrete collection enumerators inspect backing
entries directly. Accounting/governor and host-capability back-pointers are
excluded; this is not permission to reclaim their owners.

`MemoryStorageWalk` preflights a fixed queue and identity cycle table through the
existing temporary reservation. Its conservative shell allowance and eight-byte
reference slots fund both arrays before allocation. Constructor failure releases
that reservation. Traversal never grows its backing. Reuse clears retained
references; disposal refunds only the workspace's own reservation. Ordinary
numeric slots do not acquire an identity ledger.

Work is bounded as well as distinct-node storage: root/child inspections, hash
probes and stored records consume the fixed budget. Repeated aliases, scalar
slots and empty CSV history rows cannot bypass it. Cancellation is checked during
those scans. An unknown shape or exhausted budget returns an explicit incomplete
result; it cannot silently become a complete graph. Incomplete traversal may
retain visited references until reuse/disposal. Cancellation or another exception
clears them before propagation. This class has no governor-relief call site.

The field review corrected omitted Unicode rune caches, decoded text-reader
windows, and actual type base/MRO collections beside their tuple copies. It also
added protocol side-list references. A reversed iterator exposes its single named
delegate receiver; anonymous captures and multicast delegates remain unsupported
shapes. These corrections affect the dormant traversal, not existing memory
refunds or Python behavior.

Focused Release validation covers sixteen new traversal cases: cyclic/aliased
containers, both dictionary storage forms, reversed receivers and frontend
captures, hostile protocols, work exhaustion without growth, exact reservation
funding, constructor denial, cancellation before and during scanning, reusable
cleanup, Unicode caches, primed/released reader windows, dictionary views,
exception cycles, descriptor documentation, retained type bases, multicast/
anonymous captures, and empty CSV rows. Warm repeat traversal allocates zero
managed bytes in the two declared graph tests. This is a focused allocation
observation, not an overall performance claim. Together with existing ownership,
admission-atomicity and reader controls, all **39 cases pass** in the owned
1.061-second diagnostic including cleanup. The Release solution build passes.
All **10,924 Release cases pass** under the separate finite full-suite gate;
the exact explicit Release package passes the isolated compatibility and mediated
file consumers. [qualification.json](qualification.json) records counts, source,
production, DLL and package identities. Owned receipts preserve the
[focused diagnostic](focused-process.receipt.json),
[full suites](full-process.receipt.json) and
[package check](package-process.receipt.json). These check durations are not
benchmark results.

Completion still requires one combined invariant for active/native/callback/
pending-async roots and partially constructed working storage, all remaining
bridge shapes, ephemeron ownership associations, guest-free resource abandonment,
and a traversal-capacity rule derived from admitted storage before exhaustion.
The explicit test capacities are **not** that rule. Some described owners still
lead to unsupported contexts, parser/cursor or resource shapes, so adding the
interface does not prove their whole graph is supported. No collector may use a
partial visited set to refund storage. Only the complete invariant and existing
streaming/retention/cancellation/alias/projection gates can authorize removal of
weak handles, forced-generation relief and allocation-only retry machinery.

The fully managed stack policy and all prior runtime correctness fixes remain.
No hot-loop hook, standalone native activation array, scalar coupons/refcounts,
new protection mode, unsafe code or native probing was introduced.
