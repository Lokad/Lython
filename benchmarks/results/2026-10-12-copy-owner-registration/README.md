# Mutable deep-copy ownership

List, dictionary, set and deque copies now register their fresh memory owner
before publishing the clone in the copy memo or populating it. This matches the
existing Counter/defaultdict rule and removes four exceptions to that invariant.
Four calls move; production line count is unchanged. No new registry, tracking
tier, rollback transaction or configuration is introduced.

The original runtime falsely exhausted a 65,536-byte live-memory budget in all
four cases after repeated failed deep copies. Their hooks raise without reading
the memo, leaving partial clones unregistered. With early registration, the same
1,500-copy workloads reclaim dropped results and pass. Callbacks that retain
memo-exposed clones still exhaust the budget, preserving live-memory accounting.
These controls pass through synchronous and genuinely suspended host-I/O runs.

Registration can deny earlier, before invoking copy hooks under tight budgets.
Constructor/registration refunds, memo identity, cycles, shared references,
callback effects, cooperative cancellation, allocation preflights, host-mediated
capabilities and bounded public projection remain covered by the relevant suites.
The [specification](../../../SPEC.md) records the ownership contract.

Implementation: ebb45ab9; public lifetime tests: 5c1bf937. Qualified production
tree: 85f8b1c91cc7216a6365e6703c5e79eccce5fdfb. The
[evidence](evidence.json) records original failures, actual Debug/Release DLLs
and their build producers, source trees and terminal test receipts.

Validation passes **10,330 complete Debug tests** (2,104 white-box, 8,226 public)
and **44 focused Release checks** (6 white-box, 22 core public, 16 lifetime).
The six focused groups take 2.2–6.9 seconds including cleanup. No performance
collection or comparison lane was run, and no speedup is claimed.

Forced GC is unchanged: its reclamation dependency remains unresolved. Other
ownership paths, Counter protocol fidelity and broader benchmark work remain
separate pending items.
