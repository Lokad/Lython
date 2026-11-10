# Qualified-production CSV attribution; failed search warmup

2026-11-10, F4as_v7 Ubuntu24.04.4, .NET11RC3, qualifiedee448715 production
(coreb49e50a6, source identical through49d58627). Exact original CSV validation
script, common worker/adapter/catalog/goldens. Ordinary GC/tiering plus diagnostic
perf maps; no elapsed comparison, CPython lane or speedup claim.

CSV capture:6.176591535execution seconds,6.14guest CPU seconds,1221guest samples;
63unknown leaf IPs(5.16%). Whole batches include reset/protocol/validation beyond
execution clocks; actual guest/tiering/caller threads and raw stacks stay separate.

| Innermost observed guest phase | Samples | Share |
| --- | ---: | ---: |
| VM prefix and useful inlined work |574|47.01%|
| CSV parser/construction |176|14.41%|
| Memory admission |84|6.88%|
| Call binding |55|4.50%|
| Invocation activation, including useful work |54|4.42%|
| Native call preparation |51|4.18%|
| Operand/result publication |41|3.36%|
| Context initialization |1|0.08%|

Shares are attribution, not removable-tax estimates. The activation group mostly
contains existing work-state creation/deferred execution; source already shares
bound/local slots and reuses workspaces. This does not justify another context-
allocation project or show keyword binding alone dominates. Current record parsing
already bulk-admits fragments and adopts rows/shared keys; do not recreate it.

The second, much shorter file-search script exhausted the existing128x128-job
warmup cap before six seconds. No search samples exist. This is a setup limitation,
not evidence about search performance or a failed runtime design. A future bounded
confirmation may use512-job warmup batches with the same iteration/time guards;
CSV must not be recollected merely to fill the missing search case.

Original owner exits1, all workers joined. Its cleanup leaves three zero-length
CLR IPC nodes from the killed search worker. First explicit recovery reached that
emptiness assertion; second unnecessarily retried perf reading after transferring
ownership, which perf refused. Both failures survive. Final bounded recovery uses
the already decoded CSV stacks, proves dead PID/UID/types/exact IPC names/inodes,
retires only those nodes, unmounts RAM, verifies quiet, archives and locally checks
SHA. Original owner remains failed; no successful-campaign qualification is implied.
All source/core/raw CSV data/search failure/commands/receipts remain in the archive.
No remaining VM users or RAM fixtures; no repeated measurements.

Next selected source-level design is to consolidate invocation-only operation and
suspension handling outside the existing synchronous instruction loop, preserving
one executable engine and the qualified native shortcut. Remove repeated routing
and null/async plumbing; retain original operations, spans, ordering, cancellation,
terminal refusal and joined ownership. CPU benefit remains a hypothesis requiring
one coherent source replacement and controlled complete-script comparison.
