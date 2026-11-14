# Compiled immutable tuple construction

2026-11-14, R351: qualified `e235b18b` / frozen `71ba421c` versus corrected
`72c9a1e5`. Retain removal of repeated immutable tuple construction and its emitted
child loads/MakeTuple. The existing builder uses already-emitted constants; no
second syntax pass, evaluator, cache or ownership layer. Dynamic, mutable, starred
and side-effecting values retain their actual runtime operations.

Compiled literals use existing coarse compilation funding. Derived slices and
tuple-like concatenation use the current execution's existing known-count tuple
factory. Source-owner selection/TupleLikeOwnership, ungoverned concat and LINQ
growth/materialization disappear. Cancellation follows admission before allocation
and within copies. ZIP validates six-item timestamp shape through the original
readonly input instead of copying arbitrary invalid input. No public API changes;
immutable literal identity may be reused and per-run admission thresholds change.

| Identical complete script/phase | Allocation change | Elapsed change | Wider CPU change |
| --- | ---: | ---: | ---: |
| Invoice warm,4,096records /127,031B | −7.51% | −0.95..−0.01% | −0.87..−0.22% |
| Invoice compilation | not isolated | −1.05..+1.83% | −1.18..+1.82% |
| Invoice compile-to-result | not isolated | −2.15..+2.75% | −1.35..+2.62% |
| Fulfillment warm,4,096records /126,821B | −4.68% | −2.14..−0.70% | −1.77..−0.88% |
| Constant-return unpacking diagnostic,4,096calls | effectively flat | −14.76..−7.64% | −14.21..−8.02% |
| Captured text,3,072lines /35,840B, unchanged control | effectively flat | −2.65..+3.52% | −1.93..+2.92% |

**Useful allocation removal; modest whole-script CPU results.** Four fresh local
allocation processes use identical fixture paths per role and check all outputs
and budget cleanup. Do not compare absolute bytes across different parent paths.
Dynamic-return allocation is exactly flat. The constant-return diagnostic's CPU
direction supports keeping its ordinary tuple-return path without another helper
optimization; it is not a CPython or representative-script speedup.

Compilation/fresh candidate process spreads2.71/4.02% and unchanged text5.84%
leave CPU attribution and fresh-script performance inconclusive. All adverse
medians remain in the raw data. Retain the removed construction stage, allocation
benefit and ownership fixes with that explicit uncertainty; no universal speedup
or release-wide neutrality claim. The CPython guide still measures009d/frozen8ae.

B723 uses the quiet F4as_v7 Ubuntu24.04.4/.NET11RC3 VM, ordinary GC/tiering,
unchanged worker/optional0.4adapter, four fresh processes/job,12s full-request
warmups and five1s rotating batches. Warm clocks include mediated RAM I/O,
stdout/file validation and prior-result cleanup; exclude compilation/startup,
fixture reset/protocol and final cache teardown. Compile-to-result also includes
compilation/compiled-owner disposal; wider CPU includes reset/protocol. No CPython
timing, RSS peak, shared-budget contention or embedding-tail claim.445.93s remains
inside480s ownership; all image/auxiliary/output/budget/quiet/join/archive gates
pass, no workers or RAM fixtures remain. Raw SHA256:
`bede96e1d4be816e44013935cf85e6b33c90be5a957879ea969a77cc8a86a170`.

144internal/547public cases per actual .NET11/10 pass (1,382total), with14isolated
Python controls, actual delayed slice-bound I/O, first refusal, cancellation,
aliases and retained results after compilation cleanup. Six focused failures in
the initial prototype now pass; two copy guards also fail exact old01fd at34–36KB
versus16KBguard. Full platform/stress/installed-consumer delivery is pending.

The[initial01fd prototype](initial/README.md) remains immutable: its derived-storage
gap, six probe-environment failures/correction, allocation results, malformed
control-fixture B722 failure and four diagnostic phases are preserved. Only
discarded working bytes with exact canonical Git/SHA/path/UID/all-alias proof were
retired for staging. Current71ba/8ae images/catalogs/toolchain and254/252MiBfloors
survive. Publication/tagging remain postponed; broader performance work continues.

[Decision](decision.json),[timing](timing-summary.json),[allocation](allocation-summary.json),
[exact sources/core/tools/checks/raw observations](sources-checks-and-observations.tar.xz).
