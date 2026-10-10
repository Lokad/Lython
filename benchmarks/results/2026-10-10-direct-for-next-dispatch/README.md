# Direct synchronous ForNext dispatch

Integrated: loop time falls -3.50%/-3.73% in both replicas, against 0.5–0.9% spreads. Positional calls -2.94%/+2.49%, keyword calls -4.35%/+0.74%, empty -2.75%/-0.79%, full sort +6.91%/+1.96% and pipeline +2.57%/-4.70% remain visible. Calls and pipeline reverse; sort increases are within their retained spreads. No general call/sort/pipeline benefit is claimed. Loop/call allocation is effectively unchanged; all sort allocation observations remain visible. Accept the measured code growth for the repeated target loop improvement, without recollection or full lanes. These paired short diagnostics do not qualify independent sessions or a general Python ratio.

Twelve paired micros stop in **8.04–8.05 seconds**, including
cleanup. Separate ordinary allocation groups stop in **4.04 / 4.04 seconds**;
native-code groups in **4.04 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification waits for an infrequent, declared milestone.

## Frozen boundary and correctness

Baseline `cedda6b495335a5b831d61092eec8ac43b360128` / production `b076a8aeb27e1848ad3b7afd4ae91a56f2a7b9c8` is identical
in production to delivered green `fa0395f3`; its prepared Release inputs are
rehashed and reused. Candidate `096e33bdb4b2be7ab6111000b7e3845a67517c99` / production
`6bc4cc2110d9e02d3e110cb77e6c7eb6cfc737af` has unchanged tests `0ad697edb2ef437961e9c5bb2a7b790984152ba5` and
whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

The synchronous main switch calls an extracted ExecuteForNext helper directly.
The shared value-operation handler calls the same helper; original PeekIterator,
MoveNext/Current, yielded-box push, exhausted pop/target and common instruction
index/stack-depth handling stay intact. Each instruction's original CheckExecution,
injected-exception check and try/catch routing remain in place. Async keeps its
original path. No range/representation, numeric/operator, ownership, funding,
local/cell mirroring or host-mediation change is introduced.

Full frozen Debug passes **9,196 checks (1,439 white / 7,757 public)**; matching
VM Release passes **168 white / 296 public**, followed by six complete canonical
CPython cases, all before normal timing declarations. Existing eighteen exact
iteration cadence/exhaustion, active-disposal, denied-funding span and
fuel-before-cancellation checks remain unchanged, alongside range retained/dropped
heap ownership, generic/user protocols, generators, closures and async host tests.

## Fresh accepted-runtime CPU lead

Before the candidate, a separately declared ten-second current-loop capture
reuses the accepted baseline producer and frozen parser without rebuilding.
Its whole group stops in **14.08 seconds**, under the **30-second cap**; all **305
responses / 9,668 invocations** match complete output. It retains **1,433 samples**,
zero reported losses/missing stacks and **5.30% unresolved leaves**. Dispatch
is 37.33% exclusive, value-operation routing 6.49%, fresh integer ownership and
the combined range stepper 4.82% each, checked array stores 3.49%, observation
2.86%, control flow 2.58% and augmented evaluation 2.30%. These are diagnostic
leads, not removable shares or predicted gains. Inclusive stacks overlap; profiler,
kernel and background JIT costs remain visible. Allocation ticks are sampled,
not exact bytes by guest type. GC pairs are incomplete, so no complete GC counts
or pauses are claimed. Source/helper inputs rehash; one service terminal, three
PIDs absent and the VM lease freshly free. There is no before/after CPU claim.

## Complete-job timing

Microseconds per invocation; parentheses are IQR/median. Negative change means
faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.879 (4.6%) | 23.222 (5.7%) | 1.493 (0.4%) | -2.75% |
| Integer loop / 1 | 1088.425 (0.5%) | 1050.292 (0.6%) | 602.237 (0.2%) | -3.50% |
| Positional calls / 1 | 562.235 (1.0%) | 545.708 (0.8%) | 119.929 (0.8%) | -2.94% |
| Keyword calls / 1 | 612.216 (0.6%) | 585.592 (0.9%) | 135.630 (0.5%) | -4.35% |
| Full stable sort + output / 1 | 2605.992 (3.9%) | 2786.177 (9.6%) | 305.532 (0.9%) | +6.91% |
| ASCII pipeline / 1 | 138.305 (2.2%) | 141.862 (3.6%) | 41.463 (1.7%) | +2.57% |
| Empty / 2 | 25.310 (2.8%) | 25.111 (3.3%) | 1.555 (0.4%) | -0.79% |
| Integer loop / 2 | 1087.913 (0.9%) | 1047.288 (0.8%) | 591.658 (0.0%) | -3.73% |
| Positional calls / 2 | 566.059 (1.1%) | 580.133 (3.1%) | 119.601 (0.5%) | +2.49% |
| Keyword calls / 2 | 592.876 (1.4%) | 597.276 (1.6%) | 134.740 (0.5%) | +0.74% |
| Full stable sort + output / 2 | 2773.060 (13.6%) | 2827.412 (10.9%) | 302.540 (0.7%) | +1.96% |
| ASCII pipeline / 2 | 142.001 (1.4%) | 135.331 (2.4%) | 41.388 (0.5%) | -4.70% |

Sources, fixtures and full goldens are identical, including full stable-sort
output. Compilation precedes timing; each invocation gets fresh guest state.
Pinned CPython 3.13.16 uses PGO/LTO, -I -S and ordinary GIL/GC. Lython uses
SDK 10.0.401 / CLR 10.0.12, workstation GC, Interactive latency and ordinary
worker tiering/PGO without overrides. API, materialization and containment costs
remain inside the complete job. Short diagnostics do not qualify independent
sessions or a general language speed ratio; no control time is subtracted.

## Separate allocation diagnostic

The frozen exact Compile/Invoke helper is rehashed and reused without rebuilding.
Process-wide GC.GetTotalAllocatedBytes(precise:true) brackets 100 invocations after
16 warmups per case/pass; two passes share each role's process. All 24 rows,
2,400 measured and 384 warmup invocations check complete output, without forced
GC. These managed observations are not exact per-type guest bytes or sessions.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,400.00 | 29,400.00 | +0.00 |
| Integer loop / 1 | 1,079,432.40 | 1,079,432.40 | +0.00 |
| Positional calls / 1 | 1,146,362.72 | 1,146,362.72 | +0.00 |
| Keyword calls / 1 | 1,162,989.20 | 1,162,989.20 | +0.00 |
| Full stable sort + output / 1 | 3,517,247.84 | 3,538,184.72 | -20,936.88 |
| ASCII pipeline / 1 | 461,808.00 | 461,808.00 | +0.00 |
| Empty / 2 | 29,232.00 | 29,232.00 | +0.00 |
| Integer loop / 2 | 1,079,205.84 | 1,079,219.68 | -13.84 |
| Positional calls / 2 | 1,146,173.20 | 1,146,159.36 | +13.84 |
| Keyword calls / 2 | 1,162,821.20 | 1,162,807.36 | +13.84 |
| Full stable sort + output / 2 | 3,437,128.88 | 3,437,134.96 | -6.08 |
| ASCII pipeline / 2 | 461,808.00 | 461,808.00 | +0.00 |

## Separate native-code review

Two groups follow all ordinary timing and allocation. Only JIT disassembly flags
change; tiering/GC remain ordinary. All 24 helper rows verify complete outputs;
their allocation values are retained separately from ordinary allocation estimates.
The dump selects Execute, ExecuteValueOperation and ExecuteForNext in Lokad.Lython.
All emitted bodies and tiers remain visible;
missing tiers are not inferred. Inline summaries/call targets remain in evidence.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,099 |
| baseline | `ExecutableFrameInterpreter:ExecuteValueOperation` | Tier0 | 2,601 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 6,939 |
| baseline | `ExecutableFrameInterpreter:ExecuteValueOperation` | Instrumented Tier0 | 3,073 |
| baseline | `ExecutableFrameInterpreter:ExecuteValueOperation` | Tier1 | 1,702 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,380 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,098 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,658 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,685 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 2,667 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:ExecuteValueOperation` | Tier0 | 2,353 |
| candidate | `ExecutableFrameInterpreter:ExecuteForNext` | Tier0 | 254 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `ExecutableFrameInterpreter:ExecuteValueOperation` | Instrumented Tier0 | 2,728 |
| candidate | `ExecutableFrameInterpreter:ExecuteForNext` | Instrumented Tier0 | 359 |
| candidate | `ExecutableFrameInterpreter:ExecuteValueOperation` | Tier1 | 1,887 |
| candidate | `ExecutableFrameInterpreter:ExecuteForNext` | Tier1 | 488 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,886 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,810 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,837 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 2,691 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |

Final emitted Tier1 Frame.Execute grows from 2,667 to 2,691 bytes (+24 / 0.90%), retaining 19 PGO / 55 single-block / 5 other inlinees. Shared ExecuteValueOperation grows 1,702 to 1,887 bytes, with inline summaries 3 / 68 / 4 versus 4 / 67 / 4; the extracted ForNext helper separately emits 488 bytes (2 / 10 / 1). Early frame OSR grows 6,939 to 8,170 bytes (+1,231 / 17.74%) and 3,380 to 3,886; later bodies include candidate 2,810/2,837 versus baseline 2,658/2,685. All bodies/tiers, inline summaries and call targets remain visible. These profile-dependent sizes are not total resident code-cache costs, dynamic call counts or causal CPU estimates; the repeated short target timing supports accepting the growth.

## Audit and cleanup

The independent audit recalculates all **517 normal responses**,
request IDs/counts, medians/IQRs, hashes/goldens, frozen producers and ordinary
worker defaults. It checks gate completion before declaration and journal ordering
through the separate diagnostics. All **18 owned services** are terminal,
**58 recorded PIDs** absent, both producers/helper rehashed and the shared VM
lease freshly free. No observation is discarded or recollected.

Raw declarations, tests, journals, native listings and cleanup proof stay private
under `.git/agent-notes/direct-for-next-dispatch-20261010/`.
Maintained [evidence](evidence.json) carries audited observations and supporting
hashes. Further runtime work, original CSV/pipeline causes, external Utf8Regex
integration and milestone/secondary qualification remain pending.
