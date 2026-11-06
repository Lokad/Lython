# Frame resumption and completed returns — R21

Retain the lifecycle simplification. Pending exceptions are produced at generator
resumption or child-call completion, then routed once after pending operand
cleanup. Active delegation retains its forwarding. Remove that per-instruction
poll and `ExecutableWorkState.HasReturn`: nonnull ReturnValue already marks a
completed return, because Python None is PyNone. Check completion at control-flow
instructions, retaining the current instruction/span and disposal order. Keep real
yield/call state and every instruction cancellation/admission guard. No new
dispatch framework, cache, counter, policy or public API.

## Complete-script source pair

2026-11-06, Azure F4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Reference
is fully qualified f2ce53db runtime source, frozen core67f84196. Candidate is
90a00002 plus the archived R21 patch, corec3e65ad3. Same b94fa98c worker/host and
normal0.4 adapter/producer; three independent roles, including identical current
control. This iteration does not refresh the primary external CPython baseline.
The measured runtime source is committed as4fad63c5; later build metadata does
not replace the recorded binary identities or boundaries.

B366 completes108.57s, exit0, all quiet/golden/funding/Closed/join/fixture-cleanup
and under-lease archive/verification gates pass. No SSH during collection.
Five-second warmup/role, seven rotated150ms rounds. Warm includes mediated RAM
I/O, full validation and prior-result cleanup; fresh also includes compilation
and program disposal. Startup, staging/reset and final teardown are excluded.
Compile-only excludes execution/I/O; do not subtract independent block medians.

| B366 workload / lane | Reference | Current / identical control | Elapsed change |
| --- | ---: | ---: | ---: |
| Text/helpers / warm |2.263ms|2.137 /2.196ms|−5.59 /−3.00%|
| Text/helpers / fresh |2.300ms|2.301 /2.282ms|+0.02 /−0.79%|
| Report / warm |3.538ms|3.503 /3.448ms|−0.99 /−2.53%|
| CSV/functions / warm |4.169ms|4.106 /4.144ms|−1.50 /−0.59%|
| JSON / compile-only control |65.150µs|64.193 /63.697µs|−1.47 /−2.23%|

Warm text current-role spread2.75%, fresh0.81%; report IQR reaches4.07% and
current-role spread1.56%. The unchanged compilation control also improves, so
source-effect precision is limited. Warm text supports a modest benefit; fresh
text is essentially flat and no general report/CSV/CPU improvement is established.
Coarse process CPU remains in comparison-summary.json. Do not claim R20's possible
text regression is causally repaid or that the whole-script objective is complete.

Separate existing complete-job allocation diagnostic, sync only:32warmups/image,
five16-job batches, eight cases and baseline/current/identical-baseline images
sharing CoreLib/hash seed. All isolation/witness/golden/balance/fixture gates pass.
Allocation and admitted peaks are effectively flat; changes are within0.03%.
Busy Windows clocks are diagnostic. There is no async allocation or RSS claim.

## Source gate and limited diagnostic profile

The reused compile-only census shows85module instructions, thirteen executed
instructions per successful word plus two retained store continuations. The
`words_from_line` helper already has a three-node native body. Unified storage,
direct arguments/receivers and flat instructions already exist; another frame
wrapper or input-packet layer would duplicate completed work.

B365 captures3072complete text jobs with full stdout/file goldens,1355active-guest
leaf samples and250prefix samples. This is **limited diagnostic evidence**:
root perf report rejects data after ownership changes; the owner fails, the idle
worker is killed and normal managed shutdown/immediate final quiet gate are not
proved. Recover persistent raw data under a new exclusive lease; do not retime.
Source writers establish the lifecycle invariant independently of this sample.

The JIT text ends in its jump-table/footer, so the initial exact-footer parser
cannot match Tier1. Preserve that failure. Printed Tier1 instructions nevertheless
cover exactly the map's10,833bytes continuously from offset zero. Match the sole
same-process Tier1 range using that complete byte coverage;250/250prefix IPs map
to actual printed instructions, with no invented bytes or lowered coverage gate.
Raw code, maps, parser versions and limitations remain. The mixed prefix includes
guards/spills/dispatch/inlined work; its sample count is not removable dispatch
cost or a speedup prediction. No further diagnostic capture is scheduled here.

Original B363/B364 environment preflights also remain. B363 rejects the intended
JIT flags before warmup/capture. B364 recognizes their names but compares raw
values to the frozen worker's redacted `<present>` echo. Correct that diagnostic
validation against the actual record, keep exact launch values separately and
reject undeclared nonempty overrides. Both workers are killed before normal
managed shutdown is proved; neither has a captured batch. Their code/maps/journals
and failed shutdowns are retained. No runtime tests/assertions were weakened.

## Validation and delivery status

Both-target Release build:0warnings/errors. Focused790internal/2923public and
full ordinary4274internal/16200public pass per actual.NET11/.NET10. Existing
generator throw/close/yield-from, child failure, return/finally, real suspension,
terminal cancellation/refusal, retained aliases and cleanup checks retain their
assertions. No architecture-only test or exact-checkpoint counter was added.

Platform/original Stress/installed-package qualification belongs to the next
combined delivery checkpoint; it is not inferred from local checks. The previous
runtime f2ce53db remains fully qualified as GREEN42294911136. Publication postponed.
