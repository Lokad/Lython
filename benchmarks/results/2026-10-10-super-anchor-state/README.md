# Unused implicit-super anchor state

Rejected; candidate remains isolated: removing unused implicit-super anchor state.
positional: 16,379.20 bytes (3.13%)/16,288.96 bytes (3.11%) saved per complete job;
keyword: 16,368.24 bytes (3.12%)/16,495.04 bytes (3.15%) saved per complete job; owned
method: 16,394.16 bytes (0.46%)/16,514.56 bytes (0.46%) saved per complete job. Timing
in both retained replicas: empty: +1.71%/-0.77%; loop: +2.68%/+1.28%; positional:
-3.34%/-2.82%; keyword: +0.83%/-0.39%; owned method: +3.83%/-0.02%; full sort/output:
-5.60%/+10.70%; pipeline: +0.57%/+0.41%. The owned-method savings fall below the
prospective 0.5% floor required for each call case in both passes, so production remains
unchanged. Repeated benefits beyond both retained spreads: positional. No repeated
material timing regression beyond the declared max(2%, spread) bound. All controls and
allocation observations are retained; no full lane or qualified Python multiplier.

Fourteen paired micros stop in **8.04–8.06 seconds**, including
cleanup. Separate ordinary allocation groups stop in **4.03 / 4.03 seconds**;
native-code groups in **4.04 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification waits for an infrequent, declared milestone.

## Frozen boundary and correctness

Baseline `6ecc00b21b6d16c2cb962655dd4ae527e745d574` / production `0b54042760d918797c99b239467f2c63fb0c5c1a` is identical
in production to delivered green `9e0aaf3d`; its prepared Release inputs are
rehashed and reused. Candidate `b11ce14eca5cc097b349d18bf2206dbd7fc89583` / production
`53e49bd00a6639a6b61d64d792c762b809ee891d` has tests `5113f74ad86a92c14c301ab88ef1b35fa0b6cdf0` and unchanged
whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

The isolated candidate removes the typed implicit-super anchor field, its uncalled
PyType setter overload and the unreachable typed-anchor fallback in Super. Current
production binding supplies an ExecutableCell, and that field's only private write
was the uncalled overload. Source/test call sites and public reachability are
reviewed before editing. No supported function, method or descriptor sets that
state. Removing it retains actual contexts, lexical cell identity/live Value,
receiver binding, missing-cell precedence, errors/spans and the explicit one/two-
argument descriptor paths. Their existing 64-byte logical funding remains intact;
all checkpoints, leases, active exceptions, depth rollback and host mediation stay.
No cache, fee change or field-count allocation prediction is introduced.

Independent regression efe27a44 adds 13 funding/parity checks. All 58 selected
checks (13 new plus 45 existing class boundaries) pass unchanged production first;
versioned white/public assemblies and receipts are archived. Checks cover instance,
classmethod/property and inherited defining-class super, explicit descriptor
anchor/receiver/type identity, exact 64-byte commits, denied reservation/span,
missing/empty/invalid cells, mutable cells/closures and both Run and RunAsync.
Full frozen Debug passes **9,272 (1,510 white / 7,762 public)**; VM Release passes
**239 white / 331 public**, followed by seven complete canonical CPython cases,
all before timing. The existing calls.method.medium case extends this round's
catalog; source, fixture and golden are copied unchanged from the full catalog.

Acceptance policy/code hashes precede timing: at least 0.5% allocation saving in
both passes of all three call cases, no repeated material timing regression beyond
max(2%, retained spread), and speed claims only beyond both spreads. The initial
preparation launcher draft used a six-case array; the seven-case array/modulo is
corrected before any preparation or timing, with both declarations retained.
Two inherited console messages still say twelve collections/eighteen services;
the frozen schedule, journal/PID audit and receipts verify fourteen/twenty.

## Accepted-baseline call CPU leads

Two declared captures on source 6ecc00b2 / production 0b540427, identical to green
9e0aaf3d, take **16.08 / 16.09 seconds** under 30-second caps. All **1,450 responses /
46,216 invocations** check full output. **1,305 / 1,459 samples**, zero reported
loss/missing stacks, **4.37% / 5.96% unresolved CPU leaves** remain. Execute leaf
12.34% / 13.37%, simple return 9.96% / 8.09%, Invoke 3.30% / 3.29%; keyword
BindInto leaf 4.18%. Both traces resolve context/BigInteger allocation ticks as
sampled leads, never exact type counts/bytes. Inclusive interpreter stacks contain
module work and overlapping callees. Profiler/kernel/JIT overhead and incomplete
GC pairs stay visible: no full GC counts/pauses or qualified before/after CPU
distribution. Two services terminal/six PIDs absent, inputs/helpers rehashed and
lease free. Current complete-job diagnostics below determine allocation claims.

## Complete-job timing

Microseconds per invocation; parentheses are IQR/median. Negative change means
faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.115 (7.2%) | 23.510 (3.0%) | 1.487 (0.6%) | +1.71% |
| Integer loop / 1 | 1058.219 (0.4%) | 1086.531 (0.7%) | 598.812 (0.3%) | +2.68% |
| Positional calls / 1 | 433.410 (1.0%) | 418.933 (1.4%) | 128.113 (0.5%) | -3.34% |
| Keyword calls / 1 | 458.339 (1.0%) | 462.124 (0.5%) | 137.490 (0.5%) | +0.83% |
| Owned method calls / 1 | 1783.859 (1.2%) | 1852.159 (1.6%) | 116.054 (0.2%) | +3.83% |
| Full stable sort + output / 1 | 2714.302 (1.7%) | 2562.293 (3.7%) | 300.395 (0.5%) | -5.60% |
| ASCII pipeline / 1 | 137.728 (2.4%) | 138.518 (2.2%) | 41.565 (1.0%) | +0.57% |
| Empty / 2 | 23.636 (1.6%) | 23.453 (2.4%) | 1.467 (0.3%) | -0.77% |
| Integer loop / 2 | 1060.531 (0.9%) | 1074.126 (1.1%) | 588.865 (0.0%) | +1.28% |
| Positional calls / 2 | 433.849 (0.3%) | 421.625 (0.7%) | 119.619 (0.3%) | -2.82% |
| Keyword calls / 2 | 477.229 (0.8%) | 475.372 (0.3%) | 137.105 (0.4%) | -0.39% |
| Owned method calls / 2 | 1816.537 (0.9%) | 1816.111 (2.0%) | 121.847 (0.2%) | -0.02% |
| Full stable sort + output / 2 | 2552.283 (12.2%) | 2825.436 (3.5%) | 300.600 (0.6%) | +10.70% |
| ASCII pipeline / 2 | 139.643 (2.3%) | 140.222 (2.9%) | 41.555 (0.9%) | +0.41% |

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
16 warmups per case/pass; two passes share each role's process. All 28 rows,
2,800 measured and 448 warmup invocations check complete output, without forced
GC. These managed observations are not exact per-type guest bytes or sessions.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,392.00 | 29,384.00 | +8.00 |
| Integer loop / 1 | 1,079,424.40 | 1,079,416.40 | +8.00 |
| Positional calls / 1 | 523,752.96 | 507,373.76 | +16,379.20 |
| Keyword calls / 1 | 524,016.16 | 507,647.92 | +16,368.24 |
| Owned method calls / 1 | 3,591,129.92 | 3,574,735.76 | +16,394.16 |
| Full stable sort + output / 1 | 3,513,878.00 | 3,497,478.00 | +16,400.00 |
| ASCII pipeline / 1 | 461,800.00 | 461,792.00 | +8.00 |
| Empty / 2 | 29,224.00 | 29,216.00 | +8.00 |
| Integer loop / 2 | 1,079,210.64 | 1,079,176.00 | +34.64 |
| Positional calls / 2 | 523,557.28 | 507,268.32 | +16,288.96 |
| Keyword calls / 2 | 523,950.96 | 507,455.92 | +16,495.04 |
| Owned method calls / 2 | 3,590,367.76 | 3,573,853.20 | +16,514.56 |
| Full stable sort + output / 2 | 3,420,711.92 | 3,404,311.92 | +16,400.00 |
| ASCII pipeline / 2 | 461,800.00 | 461,792.00 | +8.00 |

## Separate native-code review

Two groups follow all ordinary timing and allocation. Only JIT disassembly flags
change; tiering/GC remain ordinary. All 28 helper rows verify complete outputs;
their allocation values are retained separately from ordinary allocation estimates.
The dump selects Execute, ParentContext/class-cell/binding helpers and simple-return/ExecuteBody methods in Lokad.Lython.
All emitted bodies and tiers remain visible;
missing tiers are not inferred. Inline summaries/call targets remain in evidence.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| baseline | `ExecutionContext:get_ParentContext` | Tier0 | 61 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Tier0 | 184 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,821 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| baseline | `PyFunction:ExecuteBody` | Tier0 | 324 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,657 |
| baseline | `ExecutionContext:get_ParentContext` | Instrumented Tier0 | 61 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Instrumented Tier0 | 210 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| baseline | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| baseline | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| baseline | `ExecutionContext:get_ParentContext` | Tier1 | 39 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Tier1 | 156 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Tier1 | 820 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Tier1 | 1,342 |
| baseline | `PyFunction:ExecuteBody` | Tier1 | 201 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 4,940 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,498 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,159 |
| candidate | `ExecutionContext:get_ParentContext` | Tier0 | 61 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Tier0 | 184 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,904 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| candidate | `PyFunction:ExecuteBody` | Tier0 | 324 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,652 |
| candidate | `ExecutionContext:get_ParentContext` | Instrumented Tier0 | 61 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Instrumented Tier0 | 210 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| candidate | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| candidate | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| candidate | `ExecutionContext:get_ParentContext` | Tier1 | 39 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Tier1 | 156 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Tier1 | 817 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Tier1 | 1,346 |
| candidate | `PyFunction:ExecuteBody` | Tier1 | 201 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 4,954 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,464 |

Emitted Tier1 bodies: ExecutableFrameInterpreter:Execute: baseline 4940 bytes; candidate 4954 bytes. PyFunctionBinding:EnterInvocationFrame: baseline 820 bytes; candidate 817 bytes. TryExecuteSimpleReturn: baseline 4498 bytes; candidate 4464 bytes. get_ParentContext: baseline 39 bytes; candidate 39 bytes. PyFunctionBase:EnterInvocationFrame: baseline 156 bytes; candidate 156 bytes. PyExecutableFunction:ExecuteBody: baseline 196 bytes; candidate 196 bytes. All emitted tiers, inline summaries and call targets remain retained. The selected dump does not measure the removed Super fallback directly; missing emissions are not zero-cost evidence. Sizes do not establish resident code-cache cost or CPU causality. The acceptance decision uses complete-job allocation and timing evidence.

## Audit and cleanup

The independent audit recalculates all **597 normal responses**,
request IDs/counts, medians/IQRs, hashes/goldens, frozen producers and ordinary
worker defaults. It checks gate completion before declaration and journal ordering
through the separate diagnostics. All **20 owned services** are terminal,
**66 recorded PIDs** absent, both producers/helper rehashed and the shared VM
lease freshly free. No observation is discarded or recollected.

Raw declarations, tests, journals, native listings and cleanup proof stay private
under `.git/agent-notes/super-anchor-state-20261010/`.
Maintained [evidence](evidence.json) carries audited observations and supporting
hashes. Further runtime work, original CSV/pipeline causes, external Utf8Regex
integration and milestone/secondary qualification remain pending.
