# Current qualified call/transport attribution — R189

2026-11-10; exact qualified3a413c59 runtime, frozen26d43e66, source unchanged on
fbc88ca5. Common worker/adapter0.4/dependencies and same R186 CSV/text scripts,
fixtures and output goldens. F4as_v7 Ubuntu24.04.4, .NET11RC3, stockGC/tiering plus
diagnostic perf maps. Two6second warmups/captures, total11.888execution seconds.
No elapsed comparison, CPython lane, startup or compilation measurement.

| Workload | Guest samples | Unknown leaves | Innermost observed groups |
| --- | ---: | ---: | --- |
| CSV validation | 1193 | 5.95% | VM prefix 36.46%, CSV parser 16.09%, CSV records 8.63%, memory admission 5.70%, operand/result publication 4.44% |
| Text summary | 1145 | 4.89% | VM prefix 33.28%, text kernels 21.66%, dictionary/subscript 16.33%, native call preparation 5.85%, scalar operators 5.33% |

Complete groups/leaves/thread deltas: [attribution](attribution.json), exact clocks:
[capture summary](capture-summary.json). Raw stacks/maps/core/source proof and
unknown/tiering/caller samples remain in [the archive](observations.tgz). Shares
include useful inlined work, truncated stacks and wider batch reset/protocol/output
checks; they are not removable-tax estimates. Current prefix leaf itself has
175CSV/250text samples (14.67%/21.83%), including useful inlined operations.
CSV binding43/1193 (3.60%), activation50 (4.19%) and operand/publication53 (4.44%)
remain, but text has no observed binding/frame category. Builtin setup is negligible.
This does not justify another broad context or builtin-metadata project.

Offline classification corrects actual names: ExecutableValueStorage, async nested
CSV record/source methods, and dictionary/subscript mutation. The original collector
and raw trace remain unchanged; complete top leaves remain available independently
of categories. The old collector's10second aggregate assertion conflicted with two
planned6second captures: align to its documented20second aggregate, retaining each
capture<10s,120sowner,30sstop/30smargin,20sreads and frame bounds. No failed small-
search warmup is retried or discarded. Source gate and exact tooling are in
[preparation](preparation.tar.xz).

Both workers verify before/after and close/join; quiet gates, temporary-map retirement,
RAM unmount, persistent archive-under-lease and localSHA verification pass. Terminal
status is cached. Recovery retires only the withdrawn b713prototype and duplicate
compressed archive after exact canonical tracked copies/all aliases are proved;
qualified26d baseline survives. No active users, RAM fixtures or abandoned work.

Next source/design gate: native dictionary accumulation in ordinary CSV/text
aggregation. Dictionary/subscript work is16.33%text plus associated native call,
scalar and transport work. Existing get/update performs separate hash/probe and
opcode/result transport. Assess a single prepared native update, reusing existing
native-table publication and compiler call facts. Exact native dictionary/string
keys, small exact integers, same admitted owner/capacity and no protocols are
required before effects; dynamic/wide/growth cases keep existing execution before
any admission or mutation. Preserve evaluation order, shadowing, Python errors,
retained aliases, current-token scans, managed call protection and first refusal.

No native table optimization is delivered or speedup forecast here. This selects
one coherent displaced-work milestone, not another keyword cache, scalar sidecar,
argument arena or generic routing framework. The existing engine, current ownership
and real dynamic operations remain. Larger performance and regex tracks stay active.
