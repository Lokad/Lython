# Current text-storage source gate

2026-11-04, committedae9b82b7 production source, frozen core
`67373996909a8b55239d5895e4a7ab7b6f13a5b53f46627fd1b61e1afb61aa7a`.
This is a bounded design gate, not a runtime change, timing comparison or completed
performance milestone. Full list delivery41793317194 is independent and running.

Reuse the existing checked worker and EventPipe collector on the quiet Azure
F4as_v7/Ubuntu24.04.4/.NET11 RC3 VM. One complete text-helper script, four-second
warmup then256 invocations; active diagnostic window0.823s, owned service15.773s
below90s bound. Both original quiet gates, source/Ready identities, full stdout/file
goldens, worker/collector normal close/joins and fixture removal pass. Ordinary
GC/tiering, no forced collection, runtime instrumentation, perf maps or JIT dump.
The trace is converted offline locally because VM disk headroom is limited.

## Current observations

5,813 allocation ticks, approximately619.99MB attributed on the actual named guest
thread; no missing Lython allocation stacks. These are sampled allocation origins
during complete checked scripts, not exact object sizes, CPU percentages or live
heap/reachability tracing. Other worker threads and transport/check work are not
all included. The reader retains top12 origins/type and all type totals.

| Sampled type/origin | Approximate sampled share |
| --- | ---: |
| All PyString construction |36.68%|
| PyString construction under whitespace split |18.37%|
| All byte arrays |19.92%|
| Byte arrays under whitespace split |8.06%|
| Object arrays under split/list construction |10.94%|
| List-construction temporary reservations under split |4.85%|
| Split output temporary reservation |4.16%|

Subrows overlap their type totals and must not be added twice. String objects
remain necessary for Python behavior; not all reported construction is removable.
The8.06% array origin includes payload as well as headers. This supports examining
fragment storage; it does not predict an8% CPU/allocation gain. B203's synchronous
text1.2-2.9% elapsed cost remains accepted debt, not closed by these samples.

## Selected design proposal, not implemented

Assess one compact, pre-admitted UTF8 payload with ordinary immutable PyString
windows, replacing individual word arrays. Standard managed ArraySegment versus
ReadOnlyMemory has access/layout tradeoffs; avoid a new backing-owner object,
pool, string backend or storage ledger. The existing execution batch owns the
shared payload until cleanup. A child string's unpublished refund covers only
its actual metadata/cache; no parent/backing refund while siblings/aliases live.
Conservative batch retention remains explicit, with bounded host-driven streams.

PyString's `_committedCacheBytes` has one writer and describes one optional
rune-index array. Deriving its charge from that actual cache can remove the
accumulator while introducing offset-capable storage, containing normal-string
metadata cost. A private managed shape probe subsequently measures72B for the current object,
72B for the proposed segment/cache-derived shape, then72B for an unchanged current
control on actual.NET11 RC3 and.NET10.0.8. The payload is preallocated and each
object escapes through a volatile sink. This supports the metadata model; it is
not an implemented runtime, CPU result or guarantee of its future field layout.
Actual normal-string allocation/access overhead still needs validation before
promotion. Preserve preflight/rollback
of cache construction. Do not replace the cache with repeated scans or weak tables.

All direct charge consumers are bounded and inspected: default warnings create
fresh standalone strings; reader transformed windows come from private builtin
decoders; functools cache keyword labels are fresh standalone strings. General
unpublished snapshots remain actual-owned-charge snapshots, not graph tracing.
Every raw-array access in PyString is confined to that implementation and must
respect offsets after migration, including hashing/scalars/indexing/formatting.
No global ownership audit or arbitrary-reference recovery is proposed.

Complete this as one coherent representation/construction milestone: remove
per-fragment arrays and the redundant cache accumulator; leave no dormant alternate
layout/API. Preserve Unicode/whitespace/maxsplit/remainder semantics, identities,
independently escaping words, caches, first refusal and funding through all users.
Run focused string/format/codec/hash/index/ownership/cancellation checks on actual
11/10. Compare complete text BOTH APIs plus ordinary-string JSON/report controls,
source matched for B203 debt, separating allocation/elapsed/peaks. Record metadata
or CPU costs and decide debt at completion; percentages inform, not automatically
veto. Stop or refine with a precise reason if the owner/access model needs another
infrastructure layer. No new external CPython or unchanged adapter campaign.

Raw archive SHA256:
`0c7097567796665c736aa72dee3a23e24b2f8dfac4042c3a8173cbe0f7656d1c`.
Archive retains the original trace, checked batch/verification/close responses, conditions, identities,
gates and prospective capture plan. `allocation-reader.cs` is the unchanged offline
reader source from B170. Publication remains postponed.

`layout-cost-model.tgz` preserves the private source, initial unassigned-field
compiler diagnostics and corrected successful build. Actual per-target observations
are alongside it. No source/runtime option or package dependency changes.
