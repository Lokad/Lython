# Small method receiver argument leases

Rejected; candidate remains isolated: leasing receiver argument arrays for small runtime
method calls. positional: 0.00 bytes (0.00%)/-1.28 bytes (-0.00%) saved per complete
job; keyword: -0.24 bytes (-0.00%)/-43.76 bytes (-0.01%) saved per complete job; owned
method: 35.76 bytes (0.00%)/180.16 bytes (0.01%) saved per complete job. Timing in both
retained replicas: empty: -1.09%/-5.24%; loop: +0.09%/-1.38%; positional: +2.82%/+3.43%;
keyword: -1.72%/-3.00%; owned method: +0.72%/-5.62%; full sort/output: +5.24%/+15.35%;
pipeline: +0.67%/+0.08%. The owned-method savings fall below the prospective 0.5% owned-
method floor required in both passes, so production remains unchanged. Repeated benefits
beyond both retained spreads: keyword. Repeated material regressions:
['calls.positional.medium']. All controls and allocation observations are retained; no
full lane or qualified Python multiplier.

Fourteen paired micros stop in **8.04–8.06 seconds**, including
cleanup. Separate ordinary allocation groups stop in **4.03 / 4.03 seconds**;
native-code groups in **4.03 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification waits for an infrequent, declared milestone.

## Frozen boundary and correctness

Baseline `6ecc00b21b6d16c2cb962655dd4ae527e745d574` / production `0b54042760d918797c99b239467f2c63fb0c5c1a` is identical
in production to delivered green `4a9c1269`; its prepared Release inputs are
rehashed and reused. Candidate `3847daf4c49f4703ad10d305660e8507e36570bc` / production
`2538232e861d9950aac1b4a86a1c81866b0731a1` has tests `5d0ffd5deaa91f2abcac1b51000a2613193b0d1a` and unchanged
whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

The isolated candidate reuses existing unary/binary/ternary argument leases for
receiver calls to the sealed PyFunction, PyExecutableFunction and
PyGeneratorFunction classes with zero, one or two positional arguments. Their
common concrete Invoke twins bind raw arguments synchronously into values before
body execution; body/generator contexts own copied references. Helper leases stay
owned through awaited completion, return in finally, clear references and use
take-null/keep-first caches. General/keyword/wider calls keep their current fresh
array preparation and retainable-array contract. Descriptor lookup, receiver and
function identity/metadata, live replacement, actual contexts/services, binder
fees/errors/leases, checkpoint counts/spans/order, exception/depth rollback,
closure/generator lifetime and host mediation remain preserved. No new pool,
logical fee change, per-type byte forecast or code-size speed prediction.

Independent regressions f5316424 / c556fc2d add 29 checks. All 87 selected checks
(29 new plus 58 existing class/super boundaries) pass unchanged production first;
versioned assemblies and receipts are archived. Initial fixtures have 84 passes /
3 failures: they assumed the pure-return frame requested a memory reservation.
The corrected fixture uses actual recursion admission, passing before production
changes. Original failing receipts and versioned assemblies remain retained.
Checks include aliases, mutable methods/metadata, recursion, closure/generator
retention, positional/default/keyword/variadic errors, fuel-before-cancel, exact
checkpoint spans/counts, logical funding, admission rollback, general raw-array
retention, overlapping suspended host reads and cancellation/reuse.
Full frozen candidate Debug passes **9,301 (1,527 white / 7,774 public)**; VM
Release passes **256 white / 343 public** and seven complete canonical CPython
cases before timing. The seven-case catalog remains canonical, with unchanged
source/fixture/golden entries and identical benchmark-project tree.

Acceptance policy/code hashes precede timing: at least 0.5% allocation saving in
both owned-method passes, no repeated material timing regression beyond
max(2%, retained spread) in any case, speed claims only beyond both spreads. Module
calls are controls for this method-targeted change. Every observation is retained;
no favorable recollection or full lane. The preceding unused-anchor candidate
remains rejected and is not included in this production diff.

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

## Follow-up accepted-runtime method traces

Two separately declared captures on accepted source 6ecc00b2 / production
0b540427 follow all ordinary timing/allocation/native groups. They finish in
**16.08 / 16.08 seconds** under 30-second caps, verifying all **357 responses /
11,240 complete invocations**. **1,054 / 1,056 samples**, zero reported loss/missing
stacks, **6.83% / 7.29% unresolved CPU leaves** remain. Attribute get/set leaf
shares are **6.74% / 5.87%** and **3.42% / 4.55%**; instance lookup **4.55% / 3.88%**.
Shared async lowered statement dispatch occurs in **35.86% / 37.78% inclusive
stacks** on the synchronous worker. These stacks overlap callees; no removable
share or qualified before/after CPU claim. Profiler/kernel/JIT costs and incomplete
GC pairs stay visible, without complete GC counts/pauses. Allocation ticks resolve
argument arrays, strings, bound methods, contexts and BigInteger as sampled leads,
never exact per-type object counts or bytes. Both services are terminal, six PIDs
absent, inputs/helpers rehashed and the shared lease freshly free.

Source confirms class definitions still create lowered PyFunction bodies. Further
assessment will target shared async lowering on the synchronous method path,
eligible executable method bodies and actual attribute handling, preserving all
descriptor/override, checkpoint/funding, scope and host-async contracts. No further
runtime change or gain is claimed. The failed prefix experiment remains isolated.

## Complete-job timing

Microseconds per invocation; parentheses are IQR/median. Negative change means
faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.536 (5.1%) | 23.278 (7.0%) | 1.470 (0.5%) | -1.09% |
| Integer loop / 1 | 1118.858 (0.4%) | 1119.821 (0.8%) | 641.025 (0.3%) | +0.09% |
| Positional calls / 1 | 418.937 (1.1%) | 430.762 (1.2%) | 122.188 (0.4%) | +2.82% |
| Keyword calls / 1 | 468.783 (1.3%) | 460.697 (0.6%) | 137.791 (0.7%) | -1.72% |
| Owned method calls / 1 | 1789.988 (1.5%) | 1802.834 (1.9%) | 116.176 (0.3%) | +0.72% |
| Full stable sort + output / 1 | 2564.062 (9.5%) | 2698.341 (10.2%) | 302.698 (0.9%) | +5.24% |
| ASCII pipeline / 1 | 137.868 (2.1%) | 138.789 (1.7%) | 41.358 (0.7%) | +0.67% |
| Empty / 2 | 24.193 (4.8%) | 22.924 (4.6%) | 1.503 (0.3%) | -5.24% |
| Integer loop / 2 | 1059.265 (0.6%) | 1044.676 (0.7%) | 654.550 (3.1%) | -1.38% |
| Positional calls / 2 | 416.187 (1.0%) | 430.482 (1.3%) | 120.281 (0.5%) | +3.43% |
| Keyword calls / 2 | 471.681 (0.9%) | 457.544 (1.4%) | 135.689 (0.3%) | -3.00% |
| Owned method calls / 2 | 1890.037 (1.2%) | 1783.820 (1.3%) | 115.638 (0.3%) | -5.62% |
| Full stable sort + output / 2 | 2631.854 (7.6%) | 3035.945 (11.3%) | 305.075 (1.0%) | +15.35% |
| ASCII pipeline / 2 | 139.752 (1.7%) | 139.865 (1.7%) | 41.727 (1.1%) | +0.08% |

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
| Empty / 1 | 29,392.00 | 29,392.00 | +0.00 |
| Integer loop / 1 | 1,079,424.64 | 1,079,424.40 | +0.24 |
| Positional calls / 1 | 523,752.96 | 523,752.96 | +0.00 |
| Keyword calls / 1 | 524,015.92 | 524,016.16 | -0.24 |
| Owned method calls / 1 | 3,591,100.88 | 3,591,065.12 | +35.76 |
| Full stable sort + output / 1 | 3,512,189.68 | 3,509,940.72 | +2,248.96 |
| ASCII pipeline / 1 | 461,800.00 | 461,800.00 | +0.00 |
| Empty / 2 | 29,224.00 | 29,224.00 | +0.00 |
| Integer loop / 2 | 1,079,210.64 | 1,079,210.64 | +0.00 |
| Positional calls / 2 | 523,687.04 | 523,688.32 | -1.28 |
| Keyword calls / 2 | 523,951.60 | 523,995.36 | -43.76 |
| Owned method calls / 2 | 3,590,420.00 | 3,590,239.84 | +180.16 |
| Full stable sort + output / 2 | 3,420,736.48 | 3,420,711.92 | +24.56 |
| ASCII pipeline / 2 | 461,800.00 | 461,800.00 | +0.00 |

## Separate native-code review

Two groups follow all ordinary timing and allocation. Only JIT disassembly flags
change; tiering/GC remain ordinary. All 28 helper rows verify complete outputs;
their allocation values are retained separately from ordinary allocation estimates.
The dump selects invocation and unary/binary/ternary lease helpers, Execute,
ParentContext/class-cell/binding helpers and simple-return/ExecuteBody methods.
All emitted bodies and tiers remain visible;
missing tiers are not inferred. Inline summaries/call targets remain in evidence.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| baseline | `BoundArgumentsCallable:Invoke` | Tier0 | 364 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| baseline | `PrintCallable:Invoke` | Instrumented Tier0 | 651 |
| baseline | `ExecutionContext:get_ParentContext` | Tier0 | 61 |
| baseline | `PyFunctionBase:Invoke` | Tier0 | 421 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Tier0 | 184 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,892 |
| baseline | `PrintCallable:Invoke` | Instrumented Tier0 | 651 |
| baseline | `PyBoundMethod:Invoke` | Tier0 | 201 |
| baseline | `ObjectInitSubclassMethod:Invoke` | Tier0 | 217 |
| baseline | `PyType:Invoke` | Tier0 | 1,415 |
| baseline | `ObjectNewMethod:Invoke` | Tier0 | 316 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| baseline | `PyFunction:ExecuteBody` | Tier0 | 324 |
| baseline | `CallableInvocation:InvokeBinary` | Tier0 | 124 |
| baseline | `CallableInvocation:RentBinaryArguments` | Tier0 | 235 |
| baseline | `ObjectSetAttrMethod:Invoke` | Tier0 | 1,169 |
| baseline | `CallableInvocation:ReturnBinaryArguments` | Tier0 | 222 |
| baseline | `CallableInvocation:InvokeUnary` | Tier0 | 116 |
| baseline | `CallableInvocation:RentUnaryArguments` | Tier0 | 174 |
| baseline | `ObjectGetAttrMethod:Invoke` | Tier0 | 624 |
| baseline | `CallableInvocation:ReturnUnaryArguments` | Tier0 | 147 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `BoundArgumentsCallable:Invoke` | Instrumented Tier0 | 529 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,655 |
| baseline | `ExecutionContext:get_ParentContext` | Instrumented Tier0 | 61 |
| baseline | `PyFunctionBase:Invoke` | Instrumented Tier0 | 498 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Instrumented Tier0 | 210 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| baseline | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| baseline | `PyBoundMethod:Invoke` | Instrumented Tier0 | 245 |
| baseline | `CallableInvocation:ReturnUnaryArguments` | Instrumented Tier0 | 177 |
| baseline | `CallableInvocation:InvokeUnary` | Instrumented Tier0 | 181 |
| baseline | `CallableInvocation:RentUnaryArguments` | Instrumented Tier0 | 204 |
| baseline | `ObjectGetAttrMethod:Invoke` | Instrumented Tier0 | 718 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| baseline | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| baseline | `CallableInvocation:InvokeBinary` | Instrumented Tier0 | 199 |
| baseline | `CallableInvocation:RentBinaryArguments` | Instrumented Tier0 | 265 |
| baseline | `ObjectSetAttrMethod:Invoke` | Instrumented Tier0 | 1,335 |
| baseline | `CallableInvocation:ReturnBinaryArguments` | Instrumented Tier0 | 252 |
| baseline | `ExecutionContext:get_ParentContext` | Tier1 | 39 |
| baseline | `PyFunctionBase:Invoke` | Tier1 | 1,755 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Tier1 | 156 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Tier1 | 820 |
| baseline | `PrintCallable:Invoke` | Tier1 | 1,650 |
| baseline | `PyBoundMethod:Invoke` | Tier1 | 188 |
| baseline | `CallableInvocation:ReturnUnaryArguments` | Tier1 | 146 |
| baseline | `CallableInvocation:InvokeUnary` | Tier1 | 846 |
| baseline | `CallableInvocation:RentUnaryArguments` | Tier1 | 172 |
| baseline | `ObjectGetAttrMethod:Invoke` | Tier1 | 4,037 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Tier1 | 1,339 |
| baseline | `PyFunction:ExecuteBody` | Tier1 | 201 |
| baseline | `CallableInvocation:InvokeBinary` | Tier1 | 704 |
| baseline | `CallableInvocation:RentBinaryArguments` | Tier1 | 248 |
| baseline | `ObjectSetAttrMethod:Invoke` | Tier1 | 4,499 |
| baseline | `CallableInvocation:ReturnBinaryArguments` | Tier1 | 205 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| baseline | `ObjectInitSubclassMethod:Invoke` | Instrumented Tier0 | 247 |
| baseline | `PyType:Invoke` | Instrumented Tier0 | 1,834 |
| baseline | `ObjectNewMethod:Invoke` | Instrumented Tier0 | 346 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 4,938 |
| baseline | `BoundArgumentsCallable:Invoke` | Tier1 | 597 |
| baseline | `LambdaFunction:Invoke` | Tier0 | 424 |
| baseline | `LambdaFunction:Invoke` | Instrumented Tier0 | 469 |
| baseline | `LambdaFunction:Invoke` | Tier1 | 1,446 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,467 |
| baseline | `ObjectInitSubclassMethod:Invoke` | Tier1 | 143 |
| baseline | `PyType:Invoke` | Tier1 | 4,121 |
| baseline | `ObjectNewMethod:Invoke` | Tier1 | 498 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `BoundArgumentsCallable:Invoke` | Tier0 | 364 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `PrintCallable:Invoke` | Instrumented Tier0 | 651 |
| candidate | `ExecutionContext:get_ParentContext` | Tier0 | 61 |
| candidate | `PyFunctionBase:Invoke` | Tier0 | 421 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Tier0 | 184 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,886 |
| candidate | `PrintCallable:Invoke` | Instrumented Tier0 | 651 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| candidate | `PyBoundMethod:Invoke` | Tier0 | 790 |
| candidate | `ObjectInitSubclassMethod:Invoke` | Tier0 | 217 |
| candidate | `PyType:Invoke` | Tier0 | 1,415 |
| candidate | `ObjectNewMethod:Invoke` | Tier0 | 316 |
| candidate | `CallableInvocation:InvokeUnary` | Tier0 | 116 |
| candidate | `CallableInvocation:RentUnaryArguments` | Tier0 | 174 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| candidate | `PyFunction:ExecuteBody` | Tier0 | 324 |
| candidate | `CallableInvocation:InvokeBinary` | Tier0 | 124 |
| candidate | `CallableInvocation:RentBinaryArguments` | Tier0 | 235 |
| candidate | `ObjectSetAttrMethod:Invoke` | Tier0 | 1,169 |
| candidate | `CallableInvocation:ReturnBinaryArguments` | Tier0 | 222 |
| candidate | `CallableInvocation:ReturnUnaryArguments` | Tier0 | 147 |
| candidate | `ObjectGetAttrMethod:Invoke` | Tier0 | 624 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `BoundArgumentsCallable:Invoke` | Instrumented Tier0 | 529 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,893 |
| candidate | `ExecutionContext:get_ParentContext` | Instrumented Tier0 | 61 |
| candidate | `PyFunctionBase:Invoke` | Instrumented Tier0 | 498 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Instrumented Tier0 | 210 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| candidate | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| candidate | `PyBoundMethod:Invoke` | Instrumented Tier0 | 1,020 |
| candidate | `CallableInvocation:ReturnBinaryArguments` | Instrumented Tier0 | 252 |
| candidate | `CallableInvocation:InvokeBinary` | Instrumented Tier0 | 199 |
| candidate | `CallableInvocation:RentBinaryArguments` | Instrumented Tier0 | 265 |
| candidate | `CallableInvocation:InvokeUnary` | Instrumented Tier0 | 181 |
| candidate | `CallableInvocation:RentUnaryArguments` | Instrumented Tier0 | 204 |
| candidate | `ObjectGetAttrMethod:Invoke` | Instrumented Tier0 | 718 |
| candidate | `CallableInvocation:ReturnUnaryArguments` | Instrumented Tier0 | 177 |
| candidate | `ObjectSetAttrMethod:Invoke` | Instrumented Tier0 | 1,335 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| candidate | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| candidate | `PrintCallable:Invoke` | Tier1 | 1,650 |
| candidate | `ExecutionContext:get_ParentContext` | Tier1 | 39 |
| candidate | `PyFunctionBase:Invoke` | Tier1 | 1,755 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Tier1 | 156 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Tier1 | 820 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `PyBoundMethod:Invoke` | Tier1 | 603 |
| candidate | `CallableInvocation:ReturnBinaryArguments` | Tier1 | 205 |
| candidate | `CallableInvocation:InvokeBinary` | Tier1 | 835 |
| candidate | `CallableInvocation:RentBinaryArguments` | Tier1 | 235 |
| candidate | `CallableInvocation:InvokeUnary` | Tier1 | 1,033 |
| candidate | `CallableInvocation:RentUnaryArguments` | Tier1 | 172 |
| candidate | `ObjectGetAttrMethod:Invoke` | Tier1 | 4,037 |
| candidate | `CallableInvocation:ReturnUnaryArguments` | Tier1 | 146 |
| candidate | `ObjectSetAttrMethod:Invoke` | Tier1 | 4,499 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Tier1 | 1,338 |
| candidate | `PyFunction:ExecuteBody` | Tier1 | 201 |
| candidate | `ObjectInitSubclassMethod:Invoke` | Instrumented Tier0 | 247 |
| candidate | `PyType:Invoke` | Instrumented Tier0 | 1,834 |
| candidate | `ObjectNewMethod:Invoke` | Instrumented Tier0 | 346 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 3,879 |
| candidate | `BoundArgumentsCallable:Invoke` | Tier1 | 597 |
| candidate | `LambdaFunction:Invoke` | Tier0 | 424 |
| candidate | `ObjectInitSubclassMethod:Invoke` | Tier1 | 143 |
| candidate | `PyType:Invoke` | Tier1 | 4,333 |
| candidate | `ObjectNewMethod:Invoke` | Tier1 | 498 |
| candidate | `LambdaFunction:Invoke` | Instrumented Tier0 | 469 |
| candidate | `LambdaFunction:Invoke` | Tier1 | 1,446 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,451 |

Emitted Tier1 bodies: PyBoundMethod:Invoke: baseline 188 bytes; candidate 603 bytes. CallableInvocation:InvokeBinary: baseline 704 bytes; candidate 835 bytes. CallableInvocation:InvokeUnary: baseline 846 bytes; candidate 1033 bytes. CallableInvocation:InvokeTernary: baseline no emitted Tier1 body bytes; candidate no emitted Tier1 body bytes. ExecutableFrameInterpreter:Execute: baseline 4938 bytes; candidate 3879 bytes. PyFunctionBinding:EnterInvocationFrame: baseline 820 bytes; candidate 820 bytes. TryExecuteSimpleReturn: baseline 4467 bytes; candidate 4451 bytes. get_ParentContext: baseline 39 bytes; candidate 39 bytes. PyFunctionBase:EnterInvocationFrame: baseline 156 bytes; candidate 156 bytes. PyExecutableFunction:ExecuteBody: baseline 196 bytes; candidate 196 bytes. All emitted tiers, inline summaries and call targets remain retained. The dump includes bound invocation and lease helpers; missing emissions are not zero-cost evidence. Sizes do not establish resident code-cache cost or CPU causality. The acceptance decision uses complete-job allocation and timing evidence.

## Audit and cleanup

The independent audit recalculates all **600 normal responses**,
request IDs/counts, medians/IQRs, hashes/goldens, frozen producers and ordinary
worker defaults. It checks gate completion before declaration and journal ordering
through the separate diagnostics. All **20 owned services** are terminal,
**66 recorded PIDs** absent, both producers/helper rehashed and the shared VM
lease freshly free. No observation is discarded or recollected.

Raw declarations, tests, journals, native listings and cleanup proof stay private
under `.git/agent-notes/method-receiver-lease-20261010/`.
Maintained [evidence](evidence.json) carries audited observations and supporting
hashes. Further runtime work, original CSV/pipeline causes, external Utf8Regex
integration and milestone/secondary qualification remain pending.
