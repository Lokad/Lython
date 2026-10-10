# Direct synchronous local-variable dispatch

The change is integrated. Basic-loop execution time falls 7.07% / 8.40% in two short replicas. Call changes are small (positional -0.16% / -0.36%, keyword -0.51% / -0.85%); empty control also decreases. Sort and pipeline results are mixed and remain visible. Managed loop/call allocation is effectively unchanged. The larger generated main method is an explicit tradeoff; these observations are diagnostic, with milestone qualification still pending.

Twelve paired micros stop in **8.04–8.05 seconds**, including
cleanup. Separate allocation groups stop in **4.03 / 4.04 seconds**;
separate native-code groups in **4.04 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**. Qualification remains deferred to an infrequent, declared milestone.

## Frozen change and comparison

Baseline `a39daf4d919e557bf785348ad5286bd316f22217` / production `7f10d59e8ef989e4d90fff1a31303910bd7638c2` is identical
in production to delivered green `999d7aa9`. Its prepared Release producer is
rehashed and reused. Candidate `45937811043adb6a8d49976a9bc594a8c035d088` has production
`c9892ea6e03dc0ea04bf65d039e6d182f83d4eae`. Tests `7d3c9cfcf80a35125e723997141db47234052f6b` and the whole
benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d` remain unchanged.

The synchronous main opcode switch performs LoadLocal and StoreLocal using the
same existing helpers, bypassing the second stack-transfer switch. Both operations
stay inside their original per-instruction checkpoint, injected-exception and
exception-routing boundary. Instruction count, fuel, spans, stack bounds/growth/
clearing, local cells/mirrors, temporary funding, arbitrary precision, generators
and host mediation retain the same operations. Async execution retains the
shared handlers. No opcode fusion, pooling or unsafe array access is introduced.

The preceding current-runtime loop profile and compilation-only loop shape
motivate this boundary; they do not predict savings or describe candidate CPU
distribution. See the [preceding evidence](../2026-10-10-binding-plan-arrays/README.md).

## Complete-job timing

Microseconds per invocation; parentheses are IQR divided by median. Negative
change means faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.893 (3.8%) | 23.102 (3.3%) | 1.471 (0.4%) | -3.31% |
| Integer loop / 1 | 1303.324 (0.9%) | 1211.192 (0.8%) | 662.093 (0.0%) | -7.07% |
| Positional calls / 1 | 621.561 (1.4%) | 620.553 (0.8%) | 124.861 (0.5%) | -0.16% |
| Keyword calls / 1 | 675.574 (1.3%) | 672.126 (1.1%) | 138.486 (0.3%) | -0.51% |
| Full stable sort + output / 1 | 2673.188 (6.8%) | 2778.747 (9.3%) | 305.693 (0.3%) | +3.95% |
| ASCII pipeline / 1 | 137.653 (2.9%) | 139.942 (2.7%) | 41.566 (0.1%) | +1.66% |
| Empty / 2 | 23.345 (4.5%) | 22.973 (7.7%) | 1.476 (0.4%) | -1.59% |
| Integer loop / 2 | 1318.726 (0.7%) | 1207.973 (0.6%) | 631.526 (0.1%) | -8.40% |
| Positional calls / 2 | 642.193 (0.7%) | 639.881 (1.1%) | 122.727 (0.3%) | -0.36% |
| Keyword calls / 2 | 665.122 (0.8%) | 659.485 (0.6%) | 139.075 (0.3%) | -0.85% |
| Full stable sort + output / 2 | 2919.615 (19.6%) | 2679.982 (3.7%) | 304.268 (0.4%) | -8.21% |
| ASCII pipeline / 2 | 139.274 (1.6%) | 138.672 (3.5%) | 41.392 (0.3%) | -0.43% |

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
| Full stable sort + output / 1 | 3,536,904.08 | 3,519,816.08 | +17,088.00 |
| ASCII pipeline / 1 | 461,808.00 | 461,808.00 | +0.00 |
| Empty / 2 | 29,232.00 | 29,232.00 | +0.00 |
| Integer loop / 2 | 1,079,197.84 | 1,079,211.68 | -13.84 |
| Positional calls / 2 | 1,146,165.20 | 1,146,165.20 | +0.00 |
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
| baseline | WorkItem.Execute | Tier0 | 365 |
| baseline | Frame.Execute | Instrumented Tier0 | 2,691 |
| baseline | ExecuteStackTransfer | Tier0 | 2,876 |
| baseline | Frame.Execute | Tier1-OSR | 1,811 |
| baseline | ExecuteStackTransfer | Instrumented Tier0 | 3,482 |
| baseline | ExecuteStackTransfer | Tier1 | 2,706 |
| baseline | Frame.Execute | Tier1-OSR | 1,868 |
| baseline | WorkItem.Execute | Instrumented Tier0 | 379 |
| baseline | Frame.Execute | Instrumented Tier0 | 2,690 |
| baseline | Frame.Execute | Tier1-OSR | 1,858 |
| baseline | Frame.Execute | Tier1-OSR | 1,873 |
| baseline | Frame.Execute | Tier1 | 1,774 |
| baseline | WorkItem.Execute | Tier1 | 225 |
| candidate | WorkItem.Execute | Tier0 | 365 |
| candidate | Frame.Execute | Instrumented Tier0 | 3,099 |
| candidate | ExecuteStackTransfer | Tier0 | 2,876 |
| candidate | Frame.Execute | Tier1-OSR | 6,939 |
| candidate | Frame.Execute | Tier1-OSR | 3,387 |
| candidate | ExecuteStackTransfer | Instrumented Tier0 | 3,481 |
| candidate | WorkItem.Execute | Instrumented Tier0 | 379 |
| candidate | Frame.Execute | Instrumented Tier0 | 3,098 |
| candidate | Frame.Execute | Tier1-OSR | 2,656 |
| candidate | Frame.Execute | Tier1-OSR | 2,675 |
| candidate | ExecuteStackTransfer | Tier1 | 2,584 |
| candidate | Frame.Execute | Tier1 | 2,675 |
| candidate | WorkItem.Execute | Tier1 | 225 |

The final emitted Tier1 Frame.Execute body grows from 1,774 to 2,675 bytes (+901 / 50.79%). Its inline summary changes from 8 PGO / 38 single-block / 1 other inlinees to 19 / 55 / 5. Candidate Tier1 no longer has a standalone LoadLocal/Push call in its local-load arm, while StoreLocalValue and Pop remain calls. Stack-transfer Tier1 changes from 2,706 to 2,584 bytes under the changed profile. Early candidate OSR bodies include 6,939 and 3,387 bytes; all bodies and tiers remain visible. Method-size and inlining observations are profile-dependent, not code-cache totals or causal savings estimates. The loop measurements justify this limited expansion; broader dispatch expansion requires a separate experiment.

## Verification and cleanup

Matching Debug probe and frozen full Debug suite pass **9,165 checks**
(1,408 white / 7,757 public) before declarations. Matching VM Release checks
pass **128 white / 286 public**, followed by all six complete-output CPython cases.
Existing suites cover functions/generators, closure/local mirrors, argument errors,
exceptions, cancellation, budgets and sync/async host execution. No new test
mirrors the two existing helper calls.

Independent audit recalculates all **513 normal responses**, medians,
IQRs, output hashes, request IDs/counts, frozen identities/defaults and both
diagnostic groups. Journals verify prospective declarations and scheduling.
Both producers and the reused helper rehash; **18 owned services** are terminal,
**58 recorded PIDs** absent and the shared VM lease freshly free. Every observation
is retained without recollection.

Raw declarations, tests, journals, native listings and cleanup proof remain private
in `.git/agent-notes/direct-local-dispatch-20261010/`. Maintained
[evidence](evidence.json) contains audited observations and supporting hashes.
Further runtime costs, original CSV/pipeline causes, external Utf8Regex integration
and milestone/secondary qualification remain pending.
