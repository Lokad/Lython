# Current config and file-search CPU attribution

R131/B491, 2026-11-09, committed459813a3/core1be2. Two roughly six-second
whole-batch CPU-clock samples after six-second warmups, existing checked workers,
normal GC/tiering, quiet F4as_v7/.NET11RC3. No elapsed comparison or new harness.
Sampling includes fixture reset/protocol outside worker execution clocks, plus
timed validation; distinguish actual caller/guest threads and their stacks.

| Thread group | Config samples | Search samples |
| --- | ---: | ---: |
| Named guest execution worker |549 |875 |
| Caller/other workers |561 |300 |
| Tiered JIT |23 |55 |

Original active-worker filter included a search Tiered JIT thread above0.25CPU s.
Offline classification uses actual saved names; original classifier, aggregates,
raw samples and all-thread CPU remain. No capture replay or changed elapsed results.
Config caller reset355/protocol63 samples; search reset205/protocol25. Guest unknown
leaves27/40 remain, with inlining/truncation limits. Categories below assign each
sample to its innermost observed phase and include useful operation work:

| Guest category | Config /549 | Search /875 |
| --- | ---: | ---: |
| Host/filesystem |281 |121 |
| VM prefix |129 |326 |
| Native call preparation |1 |109 |
| Native evaluation |0 |104 |
| Native guard |0 |74 |
| Memory admission |20 |63 |
| Builtin initialization |13 |11 |

Builtin construction is not dominant here; global sharing would also break mutable
type/namespace ownership. Search native guard/evaluation has136leaf samples15.54%,
or178innermost phase samples20.34%; neither is a removable overhead estimate.
Source identifies a narrower repeated phase: consumed bound-local/constant inputs
remain full native Read nodes traversed recursively for both guard and evaluation,
while ordinary executable operands already borrow those sources directly.

R132 separately selects consolidation onto that direct-source convention. Preserve
root Read metadata and mutable global reads, guard-before-effects, dynamic fallback,
actual field re-reads after admission, operation/return checks, stack protection,
funded borrows and cleanup. No new proof engine, cache, owner or frame. No CPU-gain
prediction from source alone. The prototype must remove displaced leaf nodes/read
dispatch and compare complete scripts with unchanged controls before retention.

All image/quiet/golden/Closed/zero-budget/cleanup gates pass. All users joined,
owned RAM unmounted, raw maps/traces archived under lease and verified locally.
Terminal cached; no production change for attribution. Publication postponed.

[Identity](identity.json), [selection](selection.md), [corrected attribution/raw thread CPU](attribution.json),
[receipt](archive-receipt.json), [original raw evidence](observations.tgz), [collection/offline scripts](preparation.tar.xz).
