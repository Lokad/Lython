# Current complete-report CPU attribution

2026-11-08, committed `7265538c`, frozen core `1aa68ce9`. Same maintained
JSON-to-Markdown script, 3,072 records / 189,441 B, exact full output checks,
worker and optional0.4 adapter. Azure F4as_v7 / Ubuntu24.04.4 / .NET11 RC3.
Six-second warmup, two-second settle, six-second wall CPU-clock/FP199Hz capture.
5.786 guest-clock seconds, 5.58 guest CPU seconds, **1,110 samples, 64 unknown
leaves (5.77%)**. PerfMap/JIT diagnostics are enabled: attribution, no normal
elapsed or causal speed claim. Worker validation/protocol/reset are excluded from
guest samples; mediated I/O and prior-result cleanup remain in guest execution.

| Group | Leaf samples | Samples with any matching frame |
| --- | ---: | ---: |
| Formatting | 149 (13.42%) | 270 (24.32%) |
| JSON parsing | 150 (13.51%) | 288 (25.95%) |
| Dictionary work | 147 (13.24%) | 299 (26.94%) |
| Instruction prefix | 115 (10.36%) | 979 (88.20%) |
| String storage | 28 (2.52%) | 104 (9.37%) |
| Memory admission/storage | 43 (3.87%) | 117 (10.54%) |
| Call/frame/binding routing | 44 (3.96%) | 936 (84.32%) |
| Context constructors | 8 (0.72%) | 9 (0.81%) |

Inclusive groups overlap; **do not add them or call them removable overhead**.
Inlining and frame-pointer unwind loss limit attribution. Prefix leaves include
useful inlined work. Kernel leaves account for 56 samples (5.05%). No evidence
here establishes context/argument packaging as the dominant remaining phase.

The bounded source gate selects no speculative prototype. Ordered JSON records
already fill values directly; default parsing already handles completed work in
ordinary loops. UTF-8 token windows and final-buffer formatting are established.
Do not revive the old separate native formatted evaluator: the
[R26 correctness correction](../2026-11-07-canonical-formatting/README.md) removed
upfront mutable proofs that could become stale during admission and throw CLR
casts. Preserve slower arena/control lessons as well. This profile identifies
remaining useful work; it does not justify replacing it with another engine.

Original classifier omitted actual call-frame names and counted repeated method
addresses in inclusive ranking. Corrected offline classification deduplicates
method names per stack; all raw samples and the original classifier remain.
The initial guessed catalog ID failed before collection; only the ID was corrected
to the existing unchanged report. No second capture or favorable retiming.

All core/aux/Ready/pre-post full goldens/quiet/Closed/exit/join/maps/fixture/archive
gates pass. Persistent raw/maps archived under lease and independently verified
locally. All users joined; owned RAM unmounted. R106 full delivery is independent.
No startup/RSS/embedding-tail/pending-I/O or new CPython ratio claim.

[Attribution and exact names](attribution.json), [identity](identity.json),
[receipt](archive-receipt.json), [raw traces, maps, sources and failures](observations.tar.xz).
