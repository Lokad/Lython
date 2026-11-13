# Tuple-result caller scratch: allocation gain and control regression

Uncommitted75de63a4 versus fully qualified14dd45b7/93a6d134. Actual bounded live
inputs and pending cleanup replace the unrelated global owned-slot maximum;
representative invoice now uses immediate exact tuple-return transport. Completed
helpers copy tuple operands to fixed managed caller scratch, retaining conservative
eager admission.110internal/181public Release11 focused checks pass, including
isolated stack continuations. An initial readonly-property compile failure is
corrected before freezing and preserved. Eight complete scripts pass goldens/
balances/cleanup. Original invoice allocation−6.43..−6.46%, rich invoice−5.05%;
other allocation controls effectively flat. Desktop clocks remain diagnostic.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread |
| --- | ---: | ---: | --- |
| Invoice warm |−2.93..−0.46%|−3.48..−1.56%|0.88/1.62%|
| Invoice compile + run |−5.32..−1.82%|−5.45..−1.64%|3.30/0.27%|
| Captured text warm |+1.04..+5.05%|+1.02..+5.14%|1.95/1.90%|
| Report warm |−1.65..+3.07%|−1.68..+3.82%|3.85/0.76%|

B667 four jobs180.336s,2026-11-13UTC, quietF4as_v7/Ubuntu24.04.4/fourEPYC9V45
vCPUs/16GiB/no swap, ordinary.NET11RC3 GC/tiering, fixedfec79worker/adapter0.4.
Two processes/image/five rotating500ms rounds,5swarmup(12captured). Ranges are
four paired medians, not confidence intervals. Warm includes mediated RAM-file
I/O, full stdout/file checks and prior-result cleanup; fresh adds compilation/
compiled-owner disposal. Startup/reset/staging/final cache cleanup are outside;
widerCPU includes reset/protocol. No new external CPython ratio is inferred.

All identity/golden/balance/quiet/join/persistent-SHA archive gates pass. Every
worker is joined and RAM fixtures removed. Qualified93 reference/worker/catalog
reused. Only canonically archived superseded23core and five exact duplicate
observation archives are retired from VM working storage after fresh idle-lease/
inode/UID/all-alias/hash checks. Tracked originals remain; no floor lowered.
Original insufficient-space inspection survives. Final VM free265789440B.

**Decision: remove caller scratch before retention.** Invoice improves with
supporting CPU, but text regresses1–5%. A fixed128-byte payload now inflates the
ordinary call helper even when the result is never unpacked. This is a concrete
source-level suspect, not proven attribution of the entire text change. Refine
directly into the caller's already-admitted final operand slots, tracking output
extent before any write. Delete the fixed result buffer, shared buffer declaration
and duplicate span-publication helper; restore the existing private native-value
buffer. Preserve cleanup-before-publication, reverse operand order for unchanged
target stores, suspension/escaping behavior and first refusal. Add wider/narrower
input/output overlap regressions. One changed-source bounded comparison resolves
the control debt; retain all initial/adverse results and avoid an unchanged replay.

[Initial inactive guard](../initial/README.md), [raw](observations.tgz),
[summary](summary.json), [exact sources/bits/checks](source-checks.tar.xz).
