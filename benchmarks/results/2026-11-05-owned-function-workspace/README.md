# Execution-owned function workspace

Source d608789a/production1fa4773e plus the archived D54 patch. Measured
candidate705d2e51, reference830ceafc frozen before its commit metadata. The final
source archive includes later test-contract updates; production code is unchanged.

One actual cleared exact-capacity array is cached by the existing execution
MemoryGovernor. It remains funded while idle and is removed from the cache while
borrowed. Active/nested/suspended calls keep their original ownership through
actual cleanup. A mismatched idle array is relinquished before one admission,
never after denial as a retry. Resource cleanup releases idle capacity; late active
returns refund their own charge and cannot rebuild the cache. Zero capacity needs
no reservation. The same path serves generated dataclass methods.

Delete three ThreadStatic bound-value pools, their one/two/three-size branches,
RentBoundValues and ReturnBoundValues. No arena, slot window, saved activation
record, new owner layer, allowance, size classes, token cache or retry. Context/
interpreter objects and the original argument copy still exist. This is real
storage reuse, not cached budget credit or a completed call/frame redesign.

**Contract tradeoff:** every nonempty function buffer is now conservatively
funded64+16*capacity, including small calls/presence overflow headroom. One cleared
idle buffer retains that charge between calls. Tight shared allowances can refuse
earlier. No identical tiny-budget threshold guarantee; no refund of live users or
cumulative allocation accounting. Idle ownership ends explicitly without CLR GC.

## Separate elapsed and allocation evidence

Initial B282139.91s/five complete jobs,180s bound, workerb94, ordinary GC/tiering,
independent identical-current role and CPython3.13.16. Five-second engine warmup/job,
seven rotated~150ms samples. All quiet/source/fixture/golden/library/Closed/final-zero/
joined-worker/removed-fixture gates pass.

| Whole script/lane | Current/reference elapsed changes, two current processes | Limits |
| --- | ---: | --- |
| Ordinary CSV, warm |+0.94% /+0.72%|Current spread.22%; reference IQR1.31%; adverse small result retained|
| Ordinary CSV, compile-to-result |−2.48% /+1.45%|Current spread4.03%; inconclusive|
| Ordinary CSV, completed-host async |+0.32% /−1.92%|Current spread2.23%; inconclusive|
| Text helpers, warm |+0.10% /−1.63%|Control IQR2.64%; inconclusive|
| JSON helpers, warm |−2.17% /−1.88%|Control observation, not attribution to removed work|

The boundary includes invocation, actual mediated /dev/shm filesystem I/O, full
output/file checks and prior-result cleanup. Compilation/disposal is included only
in compile-run. Startup/staging/reset/transport/final cache/process/fixture teardown
is outside. Completed-host async does not measure genuine delayed-I/O latency.

One existing same-process complete-job diagnostic separately measures **allocation**:
B28338s within45s, unchanged ordinaryCSVwarm, three isolated images/shared CoreLib/
verified hash witnesses and actual native budgets. Six-second guest warmup/image,
2ssettle, nine rotated~200ms samples, ordinary GC/tiering, no forced collection or
profiler. All full goldens, per-job/final balances, quiet/fixture/process gates pass.

| Role | Median ms/job | Median process allocated bytes/job |
| --- | ---: | ---: |
| Original |4.8730|5,892,246|
| Candidate |4.7102|5,499,126|
| Identical original |4.7538|5,893,528|

Allocation is **6.67/6.69% lower**, about393KB/job. It includes host reset,
reflection and background work; it is not guest-only heap measurement. Paired
elapsed−3.34/−.92% has2.51% original-role spread. Current-result disposal is inside
this diagnostic, unlike prior-result disposal in workerb94; do not mix timing
boundaries or derive a CPython ratio from it. It does not erase the first warmCSV
adverse observation or establish a general/causal CPU improvement.
Reported admitted execution/root peaks are208bytes higher (5,441,191/5,541,191
versus5,440,983/5,540,983). This is conservative accounting, not physical heap/RSS.

**Retain the simpler funded workspace ownership and measured allocation saving.**
Elapsed remains mixed; no universal speedup, RSS, embedding latency or portable
performance claim. No further isolated retiming proposed; materially new evidence
can justify a later complete milestone. Larger performance objective remains open.

## Correctness and delivery

Release build26.26s/zero warnings/errors. Focus790internal/2413public per actual
.NET11/10 pass. Full4130internal/16004public per target pass. Five new ownership
controls cover reusable cleared funding, shared-root independent executions,
active storage through idle close/late return, mismatch eviction and first refusal.
Existing real suspension/cancellation/descriptor/function/closure/generator/default/
dataclass/side-effect regressions pass.

Initial32 then13 white-box checks assumed immediate scratch refund. They now prove
bounded steady cached capacity, clearing and exact recovery at explicit cleanup,
while preserving errors/spans/depths, first refusal, awaited users and retained
kwargs/input/result funding. No timeout relaxation, weakened containment, fuel or
checkpoint counters. Original failing logs remain. Final test source differs from
the measured snapshot only in these ownership-contract observations.

Retained commit bce54732 is pushed. Full platform/original stress/installed-consumer
qualification [CI42061776662](https://github.com/Lokad/Lython/actions/runs/42061776662)
is GREEN/all8: Windows/Ubuntu, actual .NET11/10, original stress and installed
consumers. [Terminal receipt](delivery-ci-status.json) cached; no unchanged replay.
Baseline1fa4773e is already fully qualified via
42051400891/all8; D54 has its own qualification above. Publication postponed.

[Contract](source-contract.md), [initial comparison](summary.json),
[allocation diagnostic](allocation/summary.json), [retained source](retained-source.tgz).
All source snapshots, raw observations, identities, adverse controls and test logs
are preserved. No new benchmark framework or producer qualification.
