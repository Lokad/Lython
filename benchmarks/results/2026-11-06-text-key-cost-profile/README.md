# Complete-text cost profile — 2026-11-06

P02/B324 is a diagnostic capture, not a performance comparison. Frozen520b3eb0
matches production1134d6e8 source; workerb94/protocol2. Quiet F4as_v7,
Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Only PerfMap and instruction-prefix
JIT listing diagnostics enabled. Six seconds guest warmup, then3072 complete
3072-line text-summary invocations:6.77guestCPU seconds/1346actual guest-TID
samples. Full goldens, final Closed/funding, joined profiler/worker and fixture
cleanup pass. [Raw capture](observations.tgz), [summary](summary.json).

All251 instruction-prefix samples map uniquely to the exact10486-byte Tier1
listing. [Mapped blocks](prefix-blocks.json), [existing mapper](map-prefix.py),
[guest leaves](guest-cpu-leaves.json). Leading blocks contain required
loop/address/guard45 samples, dispatch34, loop bound19, call target/shape16,
operand load15. These are not a removable percentage.87unknown samples and a
partial final perf-map line remain explicit; no missing samples are reassigned.
Current instruction stride32B is verified in the listing. IG292 is emitted first
as the prolog-zeroing loop at0x2f; its label is not an offset reset.

Dictionary QueryHash48/get46/SetItem45 form a material leaf cluster. TLS45,
Monitor.Enter27/Exit18 and unknown work cannot all be attributed to it. Source
inspection disproves a proposed async-machine cleanup: ExecuteTargetInstruction
already has a completed synchronous prefix. Module mirroring and frame capacity
are not assumed to be removable dominant work.

Select Q01: one contextual key hash/equality-requirement preparation walk.
Independent public probes demonstrate recursive tuple validation suppresses earlier
hash callbacks and selects a later TypeError instead of an earlier ValueError.
Remove that lookahead and separate contextual classification/hash sweeps, retaining
stored-hash copies, coarse shape preflights, semantic order, managed depth/cycles,
cancellation, terminal refusal and ownership. No persistent query plan, new storage
format, scalar ledger or cache. This capture predicts no percentage gain.

B322 failed before warmup/sampling because the inherited guard allowed PerfMap
but not the declared JitDisasm/JitStdOutFile overrides. [Original failure](failed-overrides.tgz).
B323 also failed before sampling: Ready redacts present override values as
`<present>`, while the corrected guard compared them with raw environment values.
[Second failure](failed-marker.tgz). B324 validates its explicit environment and
permits exactly the three expected Ready presence markers. Same source/worker,
profile shape and collector. All original conditions are retained. Offline raw
archive initially hit perf.data permissions; the owned file was made readable
after terminal completion, then archived without restarting the capture.

No elapsed/CPU/allocation improvement, peak working-set or embedding latency is
established. Historical profiler diagnostics never enter the current performance
table as ordinary timings. The whole-script performance objective remains open.
