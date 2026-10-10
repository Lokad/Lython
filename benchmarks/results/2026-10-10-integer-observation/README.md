# Direct exact-integer observation preflight

Integrated: targeted loop time falls -6.53%/-5.37% and positional calls -8.59%/-9.26% in both replicas, exceeding their measured spreads. Keyword calls +0.24%/-8.28%, empty +2.91%/+2.55%, sort +9.27%/-2.61% and pipeline +1.65%/-1.01% remain mixed or small against their spreads. No repeatable allocation reduction is claimed. Retain every collection; no recollection or full lane. CPython loop medians also shift between replicas (936.15 to 638.36 microseconds), so these paired short results establish neither a general Python ratio nor independent-session qualification.

Twelve paired micros stop in **8.04–8.05 seconds**, including
cleanup. Separate allocation groups stop in **4.03 / 4.03 seconds**;
separate native-code groups in **4.04 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**. Qualification remains deferred to an infrequent, declared milestone.

## Frozen change and comparison

Baseline `45937811043adb6a8d49976a9bc594a8c035d088` / production `c9892ea6e03dc0ea04bf65d039e6d182f83d4eae` is identical
in production to delivered green `bf8a6ca4`. Its prepared Release producer is
rehashed and reused. Candidate `8f7833595dbacde77a61a0476d00ab585169ec12` has production
`474906be73c88a05bd39be35f90d4b023fef99a6`. Baseline tests `7d3c9cfcf80a35125e723997141db47234052f6b` gain thirteen
exact integer-budget regressions: candidate/delivered tests `f515aa64d659ece23cb5b3defb364804c8b496f6`.
The whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d` remains unchanged.

ExecutionValueObservation.ObserveValue handles boxed BigInteger before generic
string/collection/governed/graph checks. It invokes the same memory preflight
with the same `32 + GetByteCount()` signed payload estimate and source span.
Already-owned large integers keep that preflight, and observation retains no
charge. Non-integer, rich, graph/alias, string and collection behavior keeps the
original fallback. No numeric representation, iterator, instruction/checkpoint,
fuel, stack/local/cell, ownership or host-mediation change is introduced.

Thirteen independent regressions pin exact fit / one byte short across sign and
payload boundaries, prior commitments, and an already-owned heap integer. They
pass on unchanged production first. The initial fixture retains 12 passes / one
failure: denial relief legitimately funds survivor-promotion bookkeeping. The
corrected fixture funds promotion before pinning residual budget, then checks
the original error type, exact denied size, span and retained accounting. The
baseline-passing tests integrate independently of the runtime decision.

The fresh accepted-runtime loop capture from the preceding round identifies
scalar-estimation routing as a lead; its sampled shares do not predict savings.
See the [preceding evidence](../2026-10-10-direct-jump-dispatch/README.md).

## Complete-job timing

Microseconds per invocation; parentheses are IQR divided by median. Negative
change means faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 25.125 (5.3%) | 25.857 (2.3%) | 1.589 (0.9%) | +2.91% |
| Integer loop / 1 | 1330.269 (1.3%) | 1243.342 (1.2%) | 936.147 (0.3%) | -6.53% |
| Positional calls / 1 | 650.842 (1.1%) | 594.938 (1.4%) | 128.717 (0.3%) | -8.59% |
| Keyword calls / 1 | 698.620 (0.5%) | 700.315 (1.9%) | 148.124 (1.0%) | +0.24% |
| Full stable sort + output / 1 | 2909.200 (8.6%) | 3178.820 (17.4%) | 319.996 (2.0%) | +9.27% |
| ASCII pipeline / 1 | 142.643 (4.0%) | 144.994 (3.4%) | 43.901 (3.4%) | +1.65% |
| Empty / 2 | 23.894 (3.8%) | 24.502 (3.8%) | 1.546 (0.8%) | +2.55% |
| Integer loop / 2 | 1259.055 (1.8%) | 1191.500 (3.9%) | 638.357 (1.1%) | -5.37% |
| Positional calls / 2 | 636.589 (1.1%) | 577.650 (1.5%) | 125.416 (2.6%) | -9.26% |
| Keyword calls / 2 | 666.879 (1.2%) | 611.649 (1.7%) | 135.090 (0.7%) | -8.28% |
| Full stable sort + output / 2 | 2879.554 (3.7%) | 2804.469 (9.1%) | 300.635 (0.6%) | -2.61% |
| ASCII pipeline / 2 | 139.955 (3.6%) | 138.541 (2.4%) | 41.535 (0.8%) | -1.01% |

Sources, fixtures and complete goldens are identical, including full stable-sort
output. Compilation precedes timing; each invocation gets fresh guest state.
CPython 3.13.16 uses pinned PGO/LTO, -I -S and ordinary GIL/GC. Lython uses
SDK 10.0.401 / CLR 10.0.12, workstation GC, Interactive latency and ordinary
worker tiering/PGO with no worker overrides. API/materialization/containment costs
remain in complete jobs. These short diagnostics provide neither independent
session qualification nor a general language speed ratio.

## Separate allocation diagnostic

The frozen helper is rehashed and reused without rebuilding. Exact comparison
Compile/Invoke checks complete output on every invocation. Process-wide
GC.GetTotalAllocatedBytes(precise:true) brackets 100 invocations after 16 warmups
for each case/pass; two passes share each role's process. All 24 rows, 2,400
measured and 384 warmup invocations are retained. No forced GC. These are managed
allocation observations, not exact bytes by guest type or independent sessions.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,400.00 | 29,400.00 | +0.00 |
| Integer loop / 1 | 1,079,424.64 | 1,079,424.40 | +0.24 |
| Positional calls / 1 | 1,146,354.48 | 1,146,354.72 | -0.24 |
| Keyword calls / 1 | 1,162,981.20 | 1,162,981.20 | +0.00 |
| Full stable sort + output / 1 | 3,529,035.36 | 3,509,630.80 | +19,404.56 |
| ASCII pipeline / 1 | 461,808.00 | 461,808.00 | +0.00 |
| Empty / 2 | 29,232.00 | 29,232.00 | +0.00 |
| Integer loop / 2 | 1,079,211.68 | 1,079,211.68 | +0.00 |
| Positional calls / 2 | 1,146,165.20 | 1,146,151.36 | +13.84 |
| Keyword calls / 2 | 1,162,813.20 | 1,162,813.20 | +0.00 |
| Full stable sort + output / 2 | 3,437,128.88 | 3,437,128.88 | +0.00 |
| ASCII pipeline / 2 | 461,808.00 | 461,808.00 | +0.00 |

## Separate native-code review

Two additional groups run after all normal timing and ordinary allocation groups.
Only JIT disassembly flags change; ordinary tiering/GC remain. All 24 helper rows
check complete outputs. Their allocation values are retained separately and are
not used as ordinary allocation estimates. Every emitted native body and tier is
retained below; missing tiers are not inferred. Native bytes include generated
method code, rather than a prediction of instruction cost or speed.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | ExecutionThreads+WorkItem.Execute | Tier0 | 365 |
| baseline | ExecutableFrameInterpreter.Execute | Instrumented Tier0 | 3,099 |
| baseline | ExecutionContext.ObserveValue | Tier0 | 69 |
| baseline | ExecutionServices.ObserveValue | Tier0 | 69 |
| baseline | ExecutionValueObservation.ObserveValue | Tier0 | 126 |
| baseline | ExecutionValueObservation.EstimateApproximateValueBytes | Tier0 | 151 |
| baseline | ExecutionValueObservation.EstimateApproximateValueBytesCore | Tier0 | 842 |
| baseline | ExecutableFrameInterpreter.Execute | Tier1-OSR | 6,939 |
| baseline | ExecutionContext.ObserveValue | Instrumented Tier0 | 69 |
| baseline | ExecutionServices.ObserveValue | Instrumented Tier0 | 69 |
| baseline | ExecutionValueObservation.ObserveValue | Instrumented Tier0 | 156 |
| baseline | ExecutionValueObservation.EstimateApproximateValueBytes | Instrumented Tier0 | 181 |
| baseline | ExecutionValueObservation.EstimateApproximateValueBytesCore | Instrumented Tier0 | 1,075 |
| baseline | ExecutionContext.ObserveValue | Tier1 | 394 |
| baseline | ExecutionServices.ObserveValue | Tier1 | 390 |
| baseline | ExecutionValueObservation.ObserveValue | Tier1 | 386 |
| baseline | ExecutionValueObservation.EstimateApproximateValueBytes | Tier1 | 193 |
| baseline | ExecutionValueObservation.EstimateApproximateValueBytesCore | Tier1 | 749 |
| baseline | ExecutableFrameInterpreter.Execute | Tier1-OSR | 3,377 |
| baseline | ExecutionThreads+WorkItem.Execute | Instrumented Tier0 | 379 |
| baseline | ExecutableFrameInterpreter.Execute | Instrumented Tier0 | 3,098 |
| baseline | ExecutableFrameInterpreter.Execute | Tier1-OSR | 2,666 |
| baseline | ExecutableFrameInterpreter.Execute | Tier1-OSR | 2,675 |
| baseline | ExecutableFrameInterpreter.Execute | Tier1 | 2,675 |
| baseline | ExecutionThreads+WorkItem.Execute | Tier1 | 225 |
| candidate | ExecutionThreads+WorkItem.Execute | Tier0 | 365 |
| candidate | ExecutableFrameInterpreter.Execute | Instrumented Tier0 | 3,099 |
| candidate | ExecutionContext.ObserveValue | Tier0 | 69 |
| candidate | ExecutionServices.ObserveValue | Tier0 | 69 |
| candidate | ExecutionValueObservation.ObserveValue | Tier0 | 264 |
| candidate | ExecutionValueObservation.EstimateApproximateValueBytes | Tier0 | 151 |
| candidate | ExecutionValueObservation.EstimateApproximateValueBytesCore | Tier0 | 842 |
| candidate | ExecutableFrameInterpreter.Execute | Tier1-OSR | 6,939 |
| candidate | ExecutionContext.ObserveValue | Instrumented Tier0 | 69 |
| candidate | ExecutionServices.ObserveValue | Instrumented Tier0 | 69 |
| candidate | ExecutionValueObservation.ObserveValue | Instrumented Tier0 | 309 |
| candidate | ExecutionContext.ObserveValue | Tier1 | 497 |
| candidate | ExecutionServices.ObserveValue | Tier1 | 493 |
| candidate | ExecutionValueObservation.ObserveValue | Tier1 | 489 |
| candidate | ExecutableFrameInterpreter.Execute | Tier1-OSR | 3,380 |
| candidate | ExecutionValueObservation.EstimateApproximateValueBytes | Instrumented Tier0 | 181 |
| candidate | ExecutionValueObservation.EstimateApproximateValueBytesCore | Instrumented Tier0 | 1,060 |
| candidate | ExecutionThreads+WorkItem.Execute | Instrumented Tier0 | 379 |
| candidate | ExecutableFrameInterpreter.Execute | Instrumented Tier0 | 3,098 |
| candidate | ExecutableFrameInterpreter.Execute | Tier1-OSR | 2,656 |
| candidate | ExecutableFrameInterpreter.Execute | Tier1 | 2,675 |
| candidate | ExecutionValueObservation.EstimateApproximateValueBytes | Tier1 | 193 |
| candidate | ExecutionValueObservation.EstimateApproximateValueBytesCore | Tier1 | 455 |
| candidate | ExecutionThreads+WorkItem.Execute | Tier1 | 225 |

Final emitted Tier1 Frame.Execute stays 2,675 bytes in both producers. The observation method grows from 386 to 489 bytes (+103 / 26.68%); its ExecutionContext/ExecutionServices wrappers grow 394 to 497 and 390 to 493. Estimator outer stays 193 bytes; estimator-core Tier1 changes from 749 to 455, with different PGO/inline summaries. The larger early frame OSR body stays 6,939 bytes; other emitted OSR bodies differ. All bodies, tiers, inline summaries and call targets are retained. These profile-dependent sizes do not establish total code-cache costs, dynamic call frequencies or causal speed gains. The repeated target benefit supports accepting the observed observation-code growth.

## Verification and cleanup

Matching Debug probe and frozen full Debug suite pass **9,178 checks**
(1,421 white / 7,757 public) before declarations. Matching VM Release checks
pass **141 white / 286 public**, followed by all six complete-output CPython cases.
Existing suites cover functions/generators, closure/local mirrors, argument errors,
exceptions, cancellation, budgets and sync/async host execution. The thirteen budget regressions exercise containment behavior rather than
implementation structure.

Independent audit recalculates all **515 normal responses**, medians,
IQRs, output hashes, request IDs/counts, frozen identities/defaults and both
diagnostic groups. Journals verify prospective declarations and scheduling.
Both producers and the reused helper rehash; **18 owned services** are terminal,
**58 recorded PIDs** absent and the shared VM lease freshly free. Every observation
is retained without recollection.

Raw declarations, tests, journals, native listings and cleanup proof remain private
in `.git/agent-notes/integer-observation-20261010/`. Maintained
[evidence](evidence.json) contains audited observations and supporting hashes.
Further runtime costs, original CSV/pipeline causes, external Utf8Regex integration
and milestone/secondary qualification remain pending.
