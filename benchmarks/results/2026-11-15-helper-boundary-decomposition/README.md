# Matched helper and counter decomposition

R410,2026-11-15, unchanged fully qualified1b82cd46/frozen0edc0c1c, normal0.4adapter.
Four complete text scripts share3,072lines/35,840B, mediated input/report/output,
independent goldens and command-v2 warm boundary. This is workload decomposition,
not a source-pair optimization or additive/causal cost attribution.

| Shape | Median elapsed range | Wider process CPU range | Same-bits elapsed spread |
| --- | --- | --- | --- |
| Inline casing/split |0.849–0.893ms|0.922–0.954ms|5.29%|
| Inline plus local per-line counter/assert |0.983–1.040ms|1.052–1.109ms|5.85%|
| Helper, no counter |1.137–1.144ms|1.188–1.220ms|0.58%|
| Helper plus nonlocal counter/assert |1.183–1.239ms|1.253–1.316ms|4.76%|

The helper still costs roughly0.25–0.30ms without counter work. Moving inline
controls preclude an exact penalty or removable share. The counter does not explain
most of the inline/helper difference. No CPython or cross-collection gain claimed.

Actual compiler metadata, from the exact measured core, establishes all four main
scopes unmirrored and all four dictionary loops fused. Both helper variants are
native eligible; captured counter adds one cell/closure, not a blanket fallback.
Main capacity21, helper call input start13 plus target/argument2, leaves6dead tail
slots; helper needs5. Existing ordinary array capacity can therefore fund this
synchronous child workspace without a new arena, owner, allowance or growth.
Metadata inspection compiles/disposes all scripts with budget returning to zero;
it inspects known code properties, not guest-heap reachability.

Select the next bounded prototype: borrow an unused caller tail for the entire
synchronous native helper workspace. Preserve evaluated operands and unpacked-return
windows. Before suspension, use the existing funded continuation transfer; detach
and clear the borrow after success/fault/transfer. Retain fixed managed stack storage
only when existing caller capacity is insufficient. No widened code arrays or extra
pool/controller. This removes repeated buffer construction where capacity exists;
it does not eliminate helper body dispatch or predict a speedup. Compare complete
captured/helper text and invoice/JSON controls before retention; preserve stack,
aliases, reentry, cancellation, terminal refusal and joined cleanup.

B802 finishes130.803s on quiet F4as_v7/Ubuntu24.04.4/four EPYC9V45/.NET11RC3,
16GiB/no swap, ordinary workstation GC/tiering. Two independent processes/shape,
12s full-request warmup each, five rotated0.5s batches. Full goldens/budget/quiet/
Closed/join/archive/cleanup pass; terminal cached, no workers or RAM fixtures remain.
Elapsed includes Run/mediated RAM I/O/goldens/prior-result disposal; excludes compile,
startup/reset/protocol/final cache teardown. Wider CPU includes reset/protocol.
No pending-I/O, RSS or embedding-tail claim.

Original B800 staging omits the image directory; no worker starts. B801 staging
then preserves an old absolute collector path; its owner fails before workers.
Both conditions remain, with original source/journal archived before empty RAM
fixture unmount. B802 corrects directory/path/warmup metadata only, unchanged
scripts/binaries/settings/boundary. No discarded timing exists. Existing floors
and all current images/evidence remain intact; no new retirement was needed.

[All medians](summary.json),[compiled facts](compiler-facts.json),
[actual call capacity](call-capacity-facts.json),[raw](observations.tgz),
[procedure/source/failed stages](procedure-and-catalog.tar.xz).
