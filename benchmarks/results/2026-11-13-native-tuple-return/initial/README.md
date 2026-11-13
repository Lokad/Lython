# Direct tuple return: initial inactive guard and refinement

Uncommitted R310/frozen23eb67da against fully qualified14dd45b7/93a6d134.
Compiler prepares exact immediate-unpack intent; completed native helpers can copy
tuple operands into bounded managed caller scratch, preserving eager conservative
admission and normal escaping/suspended results.110internal/180public Release11
checks pass, including new alias/refusal/cancellation/suspension and original
isolated managed-stack continuation checks. Original test-constructor build errors
are corrected before freezing and preserved.

Eight complete scripts pass full goldens/shared balances/cleanup. Allocation is
flat(within0.03% diagnostics) including invoice: this is not the intended storage
result. Current compiled facts explain why: invoice's unpack call has input slot21,
four-result intent and inline-eligible price_line's direct MakeTuple/Return, but
the global LastOwnedTemporarySlot is also21. An unrelated temporary can occupy
that slot elsewhere; the conservative guard rejects the call's actual ordinary
function/row inputs. No claimed tuple-allocation or CPU improvement.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference/candidate spread |
| --- | ---: | ---: | --- |
| Invoice warm |−0.16..+0.14%|+0.70..+1.07%|0.15/0.15%|
| Invoice compile + run |+1.11..+4.85%|−0.29..+5.04%|0.28/3.40%|
| Captured text warm |−2.64..+5.61%|−3.56..+8.10%|2.31/6.03%|
| Report warm |−2.45..+1.19%|−3.51..0%|1.75/1.90%|

B666 four jobs180.263s,2026-11-13UTC, quietF4as_v7/Ubuntu24.04.4/fourEPYC9V45
vCPUs/16GiB/no swap, ordinary.NET11RC3 GC/tiering, fixedfec79worker/adapter0.4.
Two processes/image/five rotating500ms rounds,5swarmup(12captured). Ranges span
four paired medians, not confidence intervals. Warm includes mediated RAM-file
I/O, complete stdout/file checks and prior-result cleanup; fresh adds compilation/
compiled-owner disposal. Startup/reset/staging/final cache cleanup are outside;
widerCPU includes reset/protocol. Desktop clocks are diagnostic with moving
controls, no external CPython ratio or embedding-latency claim.

Identity/golden/balance/quiet/join/verified persistent archive gates pass; no users
or RAM fixtures remain. Exact93 reference/catalog/aux reused. To preserve existing
storage floors, only archived superseded277core and two canonically verified
duplicate B665/B662 observation archives are removed from VM working storage;
tracked raw archives remain unchanged. Fresh lease/inode/UID/hash/all-alias checks
and original insufficient-space preflights survive. Final VM free265916416B.

**Decision: refine eligibility, not retime unchanged.** Inspect this bounded native
call's actual owned input slots/pending cleanup instead of rejecting it on the
whole-function maximum. Preserve cleanup-before-publication, current-token checks,
terminal refusal and managed stack admission; add an ordinary tuple-return unpack
inside a loop. Obtain source-specific focused checks and allocation confirmation
before the changed-source controlled decision. Initial observations remain valid
for these bits and do not establish the proposed transport's performance.

[Raw](observations.tgz), [summary](summary.json),
[exact sources/bits/tests/compiled facts/desktop evidence](source-checks.tar.xz).
