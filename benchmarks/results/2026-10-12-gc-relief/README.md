# GC exhaustion relief

MemoryGovernor now collects CLR generations from youngest to oldest, sweeping
existing owner snapshots after each collection and stopping when reclaimed
committed storage covers the reservation's shortfall. Already-collected targets
still sweep first. Old unreachable storage can still require a full collection;
live aliases and pending finalizers remain funded.

The unconditional parameterless full collection disappears. The bounded loop
uses CLR generations directly, with no owner-age map, tracking tier, cache,
configuration switch or new reclamation abstraction. Existing allocation-progress
and reentrancy guards, sweeps and abandoned-provider reconciliation remain.
Production adds eight code lines plus four comment lines in one file. This is
an interim improvement; weak reclamation still depends on CLR collection.

Six isolated Windows Debug controls compare the original and candidate with
identical CSV input, row counts and a 262,144-byte accounted-live-memory budget.
The columns below give generation 0/1/2 collection deltas:

| Workload | Mode | Original | Candidate |
| --- | --- | --- | --- |
| Drop 4,000 six-field rows | Run | 33 / 33 / 33 | 33 / 0 / 0 |
| Drop 4,000 six-field rows | RunAsync | 33 / 33 / 33 | 34 / 0 / 0 |
| Drop 40,000 six-field rows | Run | 344 / 344 / 344 | 537 / 2 / 0 |
| Drop 40,000 six-field rows | RunAsync | 344 / 344 / 344 | 344 / 0 / 0 |
| Retain rows until denial | Both | 1 / 1 / 1 | 3 / 2 / 1 |

Every dropped scan completes with its expected count; retained scans still deny
with MemoryError. The candidate avoids generation-2 collections in these scans.
Counts include any natural CLR collections and do not establish pause or speed
improvements. The RunAsync counter controls use an immediate host; genuine host
suspension is covered separately by the public lifetime checks.

Pinned retention can pay multiple lower-generation attempts before a final full
collection. Repeated denials without allocation progress still avoid repeated
relief. Collections continue to affect the embedding process. No latency,
allocation-saving, forced-GC-independence or benchmark-milestone claim is made.
The [specification](../../../SPEC.md) documents the automatic relief policy.

Final production: c2131a078fe93058d5e588b3373f88616f856b26; implementation:
e016b0ef. The root algorithm differs from measured private variant ec652e2d only
by four comments. [Evidence](evidence.json) records source trees, actual ten
Debug/Release DLLs and build producers, terminal test receipts, original/candidate
outcomes and hashes. Original and candidate six-case groups finish in 5.252 and
2.136 seconds including cleanup; those durations are operational bounds.

Validation passes **10,387 complete Debug tests** (2,119 white-box, 8,268 public)
and **104 focused Release checks** (28 white, 19 CSV/cancellation, 57 ownership).
Focused groups take 1.2–17.1 seconds including cleanup. New controls promote
storage to the oldest generation before dropping it, preserve retained old
aliases, and exercise mediated CSV scans with genuine delayed reads, retained
rows/fields and a first-field alias surviving source closure. Existing progress,
finalization, cancellation, copy-memo and ownership controls pass.

Host mediation, allocation preflights, cycle handling and bounded projection
remain intact. No VM collection or full benchmark lane was run. Full independence
from forced GC, wider ownership/runtime work and external Utf8Regex remain open.
