# Complete CSV helper versus inline CPU attribution

2026-11-08, R123/B482, runtime6f76/corec617. Same maintained5120record/68625B CSV
validation script and B480 independently verified inline equivalent, frozen7cf /
optional0.4. Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering, six-second
guest warmup/two-second settle/six-second CPU-clock/fp199Hz capture per script.
PerfMap/JIT diagnostics, not normal elapsed or external-baseline measurements.

| Complete script | Guest samples / CPU | Unknown leaves | Binding/workspace leaf | Frame preparation leaf | Context construction leaf |
| --- | ---: | ---: | ---: | ---: | ---: |
| CSV helper |1208 /6.08s|57 /4.72%|60 /4.97%|50 /4.14%|0|
| Equivalent inline |1174 /5.90s|69 /5.88%|0|0|0|

Groups overlap and include inlined work. Inclusive frame samples include useful
execution. Neither these shares nor the existing normal~5.1vs3.3ms gap establishes
a removable call tax. Exact tuples already borrow immutable unpack storage;
ordinary helper locals already borrow funded binding storage. Current helper uses
13canonical instructions/no exception regions and no context construction.

**No runtime prototype selected.** [Source gate](source-gate.md) records the next
coherent fixed-shape argument binding candidate, displaced work, semantic criteria
and bounded before/after controls. General protocols/default mutations, suspension,
cancellation, refusal, retained aliases and existing funding remain essential.
No new owner/arena/control carrier/evaluator/framework or speculative copy removal.

Both captures pass Ready/core/auxiliary/fullgoldens/balances/quiet/Closed/exit/join/
map-copy-hash/ownedRAM-unmount gates. Original180s owner exited1 only after cleanup:
the archive checker wrongly assumed CLR emits perfinfo.map as well as perf.map.
It emitted only perf.map, copied and hash-verified after each worker joined.
Corrected that metadata assertion under a fresh idle lease; preserved the original
failure, logs and owner exit. Same observations archived and independently verified
locally, no rerun. No workers or RAM fixtures remain. Total guest-clock12.067s.

Initial space admission refused before root/worker creation. A targeted exact-byte
hardlink operation used a fresh idle lease, permitted canonical ownership, writable
parents, all-displaced-inode aliases, SHA verification and persisted pre-mutation
plan. Recovered5537792B without losing paths/bytes; later audit verifies all17
original aliases and no temporary links. The first overly restrictive symlink
preflight refusal and correction are retained; no mutation occurred on that refusal.

[Helper attribution](attribution-helper-warm.json), [inline attribution](attribution-inline-warm.json),
[exact-core census](census.json), [decision](decision.json), [identity](identity.json),
[receipt](archive-receipt.json), [raw maps/stacks/disassembly/records](observations.tgz),
[all preparation and failures](preparation-and-corrections.tgz),
[dedup proof](dedup-proof.json), [post-audit and pre-mutation plan](space-post-audit.json).
Performance objective and independent regex track remain active; publication postponed.
