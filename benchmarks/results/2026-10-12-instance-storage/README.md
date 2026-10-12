# Instance and OrderedDict storage ownership

Governed instances now fund a 160-byte object/attribute-table shell and the
existing 64-byte attribute slots. Even empty instances can register before
initialization or copy callbacks. Attaching an ungoverned populated instance
funds every existing slot before publishing its governor; refusal leaves it
unowned and retryable. Deleting attributes keeps the shell funded.

Class construction creates a default instance only when no allocator supplies
the result, removing two discarded placeholder allocations. Fresh instances,
copies and OrderedDict results register before callbacks; allocator-returned
aliases use non-refunding registration. The free-empty-instance and unfunded
adopted-attribute exceptions disappear, along with deletion's defensive funding
guard. Existing owner snapshots, weak reclamation and reservation leases remain;
no new registry, tracking tier, rollback framework or configuration is introduced.

The original runtime falsely exhausted a 65,536-byte budget in both fixed
1,500-iteration controls: malformed OrderedDict population and failing instance
deepcopy. Early registration alone fixed only OrderedDict because empty instance
snapshots were ignored. The funded shell lets the existing path cover instances.
Dropped workloads now pass at the same caps/counts; retained instances, failed
initialization results and memo-escaped copied children still deny real growth.

CPython comparison also identified an existing copy discrepancy: callbacks
should see an empty memoized instance until all attributes have been copied.
Deep copies now stage reference pairs in one preflighted temporary array, then
publish the copied state. Failed staging releases that array's reservation;
escaped child values retain their independent owners. Shallow copies use the
direct loop. This needed semantic change adds bounded staging code; overall
production grows by **63 net lines across eight files**. No net deletion or
performance improvement is claimed.

Tradeoffs: empty objects and copies require shell/registry headroom, populated
attachment funds existing storage, and admission can deny before callbacks.
Deep-copy staging reserves 24 bytes plus 16 per attribute; publishing attributes
uses ordinary fuel/cancellation checkpoints. Tiny-budget acceptance and reported
peaks change. Memo identity, cycles, shared aliases, supported values, host
mediation, allocation preflights and bounded public projection remain validated.
The [specification](../../../SPEC.md) records these contracts.

Final production tree: 913eba9821d7238edd7190b1f6ba33146717bd60; qualified source:
d7150f6d. Validation passes **10,377 complete Debug tests** (2,117 white-box,
8,260 public), **183 focused checks per configuration** (106 white, 34 lifetime,
43 core public), and four independent CPython 3.13.2 comparisons. Focused groups
take 1.9–10.4 seconds including cleanup. The lifetime cases include genuine
delayed-host suspension, dropped/retained copies and initialization, empty
objects, allocator aliases/foreign results, memo-visible state and cycles.
Pinned-funding refusal/retry and cancellation cleanup checks pass.

The [evidence](evidence.json) pins actual binaries/build producers, original and
rejected controls, terminal receipts and compatibility outputs. Initial test-only
typing/driver mistakes, 11 stale memory snapshots and the cancellation fixture's
missing object hierarchy are retained and excluded from final qualification.
Fuel, source-span and effect assertions remain intact.

MemoryGovernor's forced-GC policy is unchanged. No timing comparison, VM
collection or full benchmark lane was run. Weak-reclamation independence,
the separate GC relief assessment and wider runtime/benchmark work remain open.
