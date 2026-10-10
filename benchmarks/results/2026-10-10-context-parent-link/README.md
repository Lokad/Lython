# Consolidated execution-context parent link

Integrated: consolidating the duplicated context parent link reduces complete-call managed allocation about 3.0–3.1%. Keyword-call time falls 2.5–5.6% in both short replicas; positional time stays within its spreads. All controls remain visible, including an unexplained loop +3.91%/-1.43% reversal. No loop/sort/pipeline speedup or qualified Python multiplier is claimed. Checked-cast cost, code growth and all emitted native bodies/tiers are retained.

Twelve paired micros stop in **8.04–8.05 seconds**, including
cleanup. Separate ordinary allocation groups stop in **4.03 / 4.03 seconds**;
native-code groups in **4.03 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification waits for an infrequent, declared milestone.

## Frozen boundary and correctness

Baseline `1304ae71868d25af130d8e145ac058b6d59dacc6` / production `7f211e6d82de3c931e65e7a6323657a934bf3cec` is identical
in production to delivered green `04caddaf`; its prepared Release inputs are
rehashed and reused. Candidate `c31195ef15c0029472d714efd3f264c79e075793` / production
`0c6b16fb0f0bf79064fcbf2633dc8b40b3659b9d` has tests `8084ebb28a78dd7774f9faf5357ef8d817609cba` and unchanged
whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

ExecutionContext now derives ParentContext through the existing immutable
ExecutionFrame.Parent using a checked cast. Its six constructors already select
the same parent for both links: ordinary/function children retain their actual
lexical parent; roots/imported modules keep no lexical parent; class bodies select
the same function-closure parent. Removing the redundant field and assignments
keeps actual contexts, namespaces, source paths, shared lexical services,
class/closure/nonlocal state, binding and every guard/checkpoint/funding operation.
Generic standalone ExecutionFrame behavior is unchanged. Field count is not used
to predict allocation or throughput; cast cost and layout are measured below.

Independent regression `71113f4f` adds nine root/module, ordinary/function,
nested-class/class-cell, comprehension namespace and class-nonlocal relationships.
All nine pass original production first; versioned baseline assembly and receipts
are archived. The frozen VM baseline producer predates these tests but has
identical baseline production. Source assessment precedes the candidate, and
baseline-passing tests establish the relevant parent and namespace relationships.
Full frozen Debug passes **9,241 checks (1,484 white / 7,757 public)**; matching
VM Release passes **213 white / 296 public**, followed by six complete canonical
CPython cases, all before normal timing declaration. Existing retention, namespace,
closure/generator, reentrant/async host, exact funding/cancellation and depth
rollback gates remain in place.

## Accepted-baseline call CPU leads

Two separately declared ten-second positional/keyword captures reuse source
1304ae71 / production 7f211e6d, identical to green 04caddaf, with no rebuilds.
Whole groups stop in **14.08 / 14.08 seconds**, under **30-second caps**;
all **1,323 responses / 42,152 invocations** match complete output. They retain
**1,319 / 1,388 samples**, zero reported losses/missing stacks and **5.00% / 3.96%
unresolved CPU leaves**. Interpreter Execute is 12.28% / 12.97% exclusive;
ExecuteExecutableCodeObject is 69.37% / 76.08% inclusive, containing module work
and overlapping callees, not a removable function-frame share.
Positional allocation ticks name context/BigInteger allocations; keyword
allocation types and sizes are unresolved (9,709 unnamed ticks, reported byte
sizes zero). A preceding private proof incorrectly labels both as typed leads;
the correction and original receipts are retained. No typed keyword or exact
per-type allocation claim is made. CPU sampling, profiler/kernel/JIT costs and
incomplete GC pairs remain visible; no complete GC count/pause or before-after
CPU distribution is qualified. Sources/helpers rehash, two services terminal,
six PIDs absent and the VM lease free. Separate whole-job allocation measurements
below determine the allocation claim.

## Complete-job timing

Microseconds per invocation; parentheses are IQR/median. Negative change means
faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.174 (2.6%) | 23.533 (3.9%) | 1.495 (0.4%) | +1.55% |
| Integer loop / 1 | 1037.897 (0.5%) | 1078.520 (0.7%) | 615.610 (0.1%) | +3.91% |
| Positional calls / 1 | 451.366 (1.2%) | 452.265 (0.8%) | 122.886 (0.6%) | +0.20% |
| Keyword calls / 1 | 528.609 (1.4%) | 498.905 (1.1%) | 135.069 (0.4%) | -5.62% |
| Full stable sort + output / 1 | 3035.302 (14.2%) | 2963.864 (12.3%) | 308.234 (0.6%) | -2.35% |
| ASCII pipeline / 1 | 137.138 (2.4%) | 139.969 (3.0%) | 41.784 (0.4%) | +2.06% |
| Empty / 2 | 23.076 (2.3%) | 23.131 (2.7%) | 1.500 (0.3%) | +0.24% |
| Integer loop / 2 | 1051.622 (0.8%) | 1036.589 (1.2%) | 613.504 (0.1%) | -1.43% |
| Positional calls / 2 | 454.377 (0.6%) | 452.236 (0.8%) | 120.371 (0.4%) | -0.47% |
| Keyword calls / 2 | 493.816 (0.4%) | 481.597 (0.8%) | 137.833 (0.4%) | -2.47% |
| Full stable sort + output / 2 | 3069.192 (20.5%) | 2710.878 (6.7%) | 304.506 (0.5%) | -11.67% |
| ASCII pipeline / 2 | 138.243 (2.5%) | 139.752 (1.9%) | 43.140 (0.5%) | +1.09% |

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
| Empty / 1 | 29,400.00 | 29,392.00 | +8.00 |
| Integer loop / 1 | 1,079,432.64 | 1,079,424.64 | +8.00 |
| Positional calls / 1 | 540,144.96 | 523,752.96 | +16,392.00 |
| Keyword calls / 1 | 540,873.28 | 524,015.92 | +16,857.36 |
| Full stable sort + output / 1 | 3,546,815.20 | 3,534,135.36 | +12,679.84 |
| ASCII pipeline / 1 | 461,808.00 | 461,800.00 | +8.00 |
| Empty / 2 | 29,232.00 | 29,224.00 | +8.00 |
| Integer loop / 2 | 1,079,218.64 | 1,079,210.64 | +8.00 |
| Positional calls / 2 | 540,080.08 | 523,557.28 | +16,522.80 |
| Keyword calls / 2 | 540,213.28 | 523,977.76 | +16,235.52 |
| Full stable sort + output / 2 | 3,437,140.40 | 3,420,725.36 | +16,415.04 |
| ASCII pipeline / 2 | 461,808.00 | 461,800.00 | +8.00 |

## Separate native-code review

Two groups follow all ordinary timing and allocation. Only JIT disassembly flags
change; tiering/GC remain ordinary. All 24 helper rows verify complete outputs;
their allocation values are retained separately from ordinary allocation estimates.
The dump selects Execute, ParentContext/class-cell/binding helpers and simple-return/ExecuteBody methods in Lokad.Lython.
All emitted bodies and tiers remain visible;
missing tiers are not inferred. Inline summaries/call targets remain in evidence.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| baseline | `ExecutionContext:get_ParentContext` | Tier0 | 28 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Tier0 | 184 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 896 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,688 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,592 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,607 |
| baseline | `ExecutionContext:get_ParentContext` | Instrumented Tier0 | 28 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| baseline | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Instrumented Tier0 | 210 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 896 |
| baseline | `ExecutionContext:get_ParentContext` | Tier1 | 5 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Tier1 | 1,078 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,035 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Tier1 | 162 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Tier1 | 1,440 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 3,885 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `ExecutionContext:get_ParentContext` | Tier0 | 61 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Tier0 | 184 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 896 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,886 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,893 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,910 |
| candidate | `ExecutionContext:get_ParentContext` | Instrumented Tier0 | 61 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| candidate | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Instrumented Tier0 | 210 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 896 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 3,879 |
| candidate | `ExecutionContext:get_ParentContext` | Tier1 | 39 |
| candidate | `ExecutionContext:TryGetLexicalClassCell` | Tier1 | 1,112 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Tier1 | 1,376 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Tier1 | 162 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,467 |

Final emitted Tier1 main Execute changes 3,885 to 3,879 bytes (-6), with inline summaries 33 PGO / 80 single-block / 12 other versus 32 / 80 / 12. ParentContext getter grows 5 to 39 bytes for its checked cast; lexical-class-cell lookup grows 1,078 to 1,112 (5 / 20 / 0 versus 5 / 21 / 0 inlinees). PyFunctionBinding.EnterInvocationFrame falls 1,440 to 1,376 with 9 / 36 / 0 inlinees in both. PyFunctionBase entry and PyExecutableFunction body remain 162 and 196 bytes. The source-unchanged simple-return evaluator changes 4,035 to 4,467 (+432 / 10.71%), with 42 / 182 / 9 versus 53 / 188 / 13 inlinees. Early main OSR remains 8,170; later baseline bodies 4,688/4,592/4,607 differ from candidate 3,886/3,893/3,910. Every emitted body/tier, inline summary and call target is retained. These profile-dependent sizes are not total resident code-cache costs or dynamic call counts, and do not prove CPU causality. The repeated complete-job results support the tradeoff; first-loop variation remains unexplained.

## Audit and cleanup

The independent audit recalculates all **517 normal responses**,
request IDs/counts, medians/IQRs, hashes/goldens, frozen producers and ordinary
worker defaults. It checks gate completion before declaration and journal ordering
through the separate diagnostics. All **18 owned services** are terminal,
**58 recorded PIDs** absent, both producers/helper rehashed and the shared VM
lease freshly free. No observation is discarded or recollected.

Raw declarations, tests, journals, native listings and cleanup proof stay private
under `.git/agent-notes/context-parent-link-20261010/`.
Maintained [evidence](evidence.json) carries audited observations and supporting
hashes. Further runtime work, original CSV/pipeline causes, external Utf8Regex
integration and milestone/secondary qualification remain pending.
