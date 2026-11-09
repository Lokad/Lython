# Current original-text CPU attribution

R140/B499,2026-11-09. One maintained original text-summary-helpers.large script,
unchanged source/fixtures/goldens, current23e production-equivalent frozena063.
Exact four changed production source hashes match the R139 freeze; other sources
unchanged froma58. Actual assembly metadataa58 is explicit; no post-commit build
or elapsed comparison implied. Full core platform/package checks pass separately;
Windows timed-hash Stress correction remains qualification work.

Existing perf collector/worker/protocol, normal GC/tiering, perf-map instrumentation,
6s warmup then6.153s sampled work. Named execution worker uses6.06CPUseconds.
1205guest samples/44caller/26tiering;59guest unknown leaves (4.90%). Correct the old
active-thread classifier before capture: Tiered JIT is not guest work. All names,
CPU deltas/raw stacks/maps remain, including unknown/inlined/truncated limitations.

| Innermost observed guest category | Samples | Share |
| --- | ---: | ---: |
| VM prefix |481|39.92%|
| Text kernels |266|22.07%|
| Dictionary |121|10.04%|
| Memory admission |80|6.64%|
| Scalar operators |77|6.39%|
| Native call preparation |71|5.89%|
| Host/filesystem |29|2.41%|
| Managed cancellation/stack |21|1.74%|
| Native evaluation/guard |26|2.16%|

Categories assign each sample to its innermost recognized frame; they are not
isolated removable costs. The prefix is246guest leaves (20.41%) but includes
inlined reads/publication/control. Native guards/evaluation are not dominant.
AddExactIntegers has40guest leaves (3.32%); earlier boxed-integer allocation shares
do not prove CPU dominance. Caller samples mainly fixture reset/host/protocol;
builtin setup is not dominant. No wholesale value/JIT/frame redesign is selected
from inclusive counts. No second capture or unchanged timing selected.

Source gate confirms native string/bytes hashing already uses current tokens and
native integer hashes, without a guest-integer roundtrip or structural traversal.
CSV already stages records and admits ordinary field storage once. Do not re-create
removed work or add method special cases. Further interpreter redesign needs a
concrete source/representation deletion and bounded comparison; these samples alone
do not establish that all prefix work can disappear.

All quiet/identity/golden/Closed/worker join/map-retirement/RAM-unmount/archive-under-
lease/local-SHA gates pass. Archive7d22390281fd742fcad202c547fea5c78aa6aebe64d516a6a95fd05216051e3f.
Terminal cached, no users/fixtures. Fresh exact one-file duplicate proof recovered
6,995,968B with all original paths/bytes/aliases/metadata preserved; floors unchanged.
Performance remains diagnostic; no startup/compilation/CPython/embedding-tail claim.

[Attribution/all thread CPU](attribution.json), [original raw profile](observations.tgz),
[source proof](core-source.json), [identity](identity.json), [archive receipt](archive-receipt.json),
[terminal status](terminal-status.json), [selection](selection.md), [space proof](space.json),
[preparation/collector/analysis](preparation.tar.xz).
