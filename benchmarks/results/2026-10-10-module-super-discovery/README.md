# Module-root implicit-super discovery

Integrated: avoiding unnecessary implicit-super discovery for unowned module-root functions reduces keyword-call time 8.8–10.2% in both short replicas, beyond their spreads. Positional time falls 4.31%/8.55%, but the first overlaps its 4.72% candidate spread, so no repeatable positional-speedup claim. Calls show no meaningful allocation reduction. Every control stays visible: sort time rises 2.31%/6.21% within baseline spreads 11.58%/10.61%; the first sort allocation pass grows 35,885.68 bytes/job (1.03%), the second is unchanged. Empty +0.98%/-0.96%, loop -0.36%/-4.89%, pipeline -3.96%/+0.53% support no general control benefit. No full lane or qualified Python multiplier.

Twelve paired micros stop in **8.04–8.05 seconds**, including
cleanup. Separate ordinary allocation groups stop in **4.03 / 4.03 seconds**;
native-code groups in **4.04 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification waits for an infrequent, declared milestone.

## Frozen boundary and correctness

Baseline `c31195ef15c0029472d714efd3f264c79e075793` / production `0c6b16fb0f0bf79064fcbf2633dc8b40b3659b9d` is identical
in production to delivered green `d7de14e2`; its prepared Release inputs are
rehashed and reused. Candidate `6ecc00b21b6d16c2cb962655dd4ae527e745d574` / production
`0b54042760d918797c99b239467f2c63fb0c5c1a` has tests `36dd7f122b1cf3d3f1e617f1db8b17eb98d22a69` and unchanged
whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

The isolated candidate skips first-receiver and lexical class-cell discovery only
for unowned functions whose closure is a module root with no ClassCell and no
class-body flag. Root/imported module parents are immutable and null. ClassCell
is privately assigned only to newly constructed class/cell scopes. Such a
function frame has no class cell to bind; namespace mirroring cannot create one.
OwnerType is read on every call, so later owner binding retains unavailable-super
behavior. Non-module closures retain their live __class__ namespace barriers and
the complete existing lookup. No general lookup result or class value is cached.
The actual function context, closure services/source, binder leases, mirroring,
argument errors/defaults, checks/spans/counts/order, depth rollback, active
exceptions and logical funding are retained. No per-function metadata is added.

Independent regressions `0fdd5faf` / `75588bbc` establish 18 module/imported-module,
receiver-layout, no-receiver, owner-binding, static/live barrier, cell-identity and
error-source/span checks before production changes. White and public versioned
assemblies and receipts are archived. The first fixture omitted a fuel budget,
giving 14 pass/4 step-count failures; the explicit-budget fixture passes unchanged
production. All original receipts remain. Public checks exercise both Run and
RunAsync. Existing class-cell/cancellation/ownership/depth suites remain included.

Full frozen Debug passes **9,259 checks (1,499 white / 7,760 public)**; matching
VM Release passes **228 white / 329 public**, followed by six complete canonical
CPython outputs, all before timing declaration. The acceptance code/policy is
hashed before timing: require a repeated call-case gain beyond both spreads and
no repeated material regression beyond spreads in calls or controls. A smaller
allocation observation alone cannot accept this throughput experiment.

## Accepted-baseline call CPU leads

Two declared positional/keyword captures reuse source c31195ef / production
0c6b16fb, identical to green d7de14e2, without rebuilding. Whole groups take
**14.07 / 18.09 seconds** under **30-second caps**. All **1,582 responses /
50,440 invocations** verify complete output. **1,394 / 6,584 samples**, zero
reported loss/missing stacks and **5.02% / 4.83% unresolved CPU leaves** remain.
Interpreter Execute is 13.27% / 16.24% exclusive; simple return 7.39% / 8.11%;
Invoke 4.66% / 6.38%; lexical class-cell lookup 1.36% / 1.63%. Keyword BindInto
is 4.78% exclusive. Inclusive interpreter stacks contain module work and overlap
callees; they do not forecast removable function-frame costs. Both fresh traces
resolve context/BigInteger allocation ticks as sampled leads, not exact counts or
per-type bytes. The earlier keyword trace stays unresolved with original receipts
and correction retained in the preceding report. Kernel/profiler/background JIT
overhead and incomplete GC pairs stay visible: no full GC counts/pauses or
qualified before/after CPU distribution. Two services terminal, six PIDs absent,
sources/helpers rehashed and lease free. Complete-job allocation follows below.

## Complete-job timing

Microseconds per invocation; parentheses are IQR/median. Negative change means
faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.200 (6.0%) | 23.427 (4.5%) | 1.465 (0.4%) | +0.98% |
| Integer loop / 1 | 1063.141 (0.4%) | 1059.337 (1.1%) | 577.654 (2.4%) | -0.36% |
| Positional calls / 1 | 462.628 (0.5%) | 442.700 (4.7%) | 120.001 (0.2%) | -4.31% |
| Keyword calls / 1 | 520.772 (0.6%) | 467.609 (1.1%) | 137.214 (1.0%) | -10.21% |
| Full stable sort + output / 1 | 2709.919 (11.6%) | 2772.435 (7.6%) | 309.245 (0.5%) | +2.31% |
| ASCII pipeline / 1 | 144.209 (2.6%) | 138.497 (2.8%) | 41.380 (0.4%) | -3.96% |
| Empty / 2 | 24.018 (1.2%) | 23.789 (2.6%) | 1.460 (0.4%) | -0.96% |
| Integer loop / 2 | 1100.343 (0.6%) | 1046.488 (1.4%) | 616.988 (0.1%) | -4.89% |
| Positional calls / 2 | 460.764 (0.9%) | 421.357 (0.3%) | 124.986 (0.4%) | -8.55% |
| Keyword calls / 2 | 508.092 (1.0%) | 463.533 (0.6%) | 139.260 (0.3%) | -8.77% |
| Full stable sort + output / 2 | 2616.133 (10.6%) | 2778.690 (6.2%) | 305.728 (0.4%) | +6.21% |
| ASCII pipeline / 2 | 137.592 (1.4%) | 138.327 (2.0%) | 41.371 (0.5%) | +0.53% |

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
| Empty / 1 | 29,392.00 | 29,392.00 | +0.00 |
| Integer loop / 1 | 1,079,424.40 | 1,079,424.64 | -0.24 |
| Positional calls / 1 | 523,753.20 | 523,752.96 | +0.24 |
| Keyword calls / 1 | 524,015.92 | 524,015.92 | +0.00 |
| Full stable sort + output / 1 | 3,490,423.28 | 3,526,308.96 | -35,885.68 |
| ASCII pipeline / 1 | 461,800.00 | 461,800.00 | +0.00 |
| Empty / 2 | 29,224.00 | 29,338.40 | -114.40 |
| Integer loop / 2 | 1,079,210.64 | 1,079,210.64 | +0.00 |
| Positional calls / 2 | 523,607.52 | 523,583.92 | +23.60 |
| Keyword calls / 2 | 523,951.12 | 523,951.84 | -0.72 |
| Full stable sort + output / 2 | 3,420,711.92 | 3,420,711.92 | +0.00 |
| ASCII pipeline / 2 | 461,800.00 | 461,800.00 | +0.00 |

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
| baseline | `ExecutionContext:get_ParentContext` | Tier0 | 61 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Tier0 | 184 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 896 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,886 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutionContext:get_ParentContext` | Instrumented Tier0 | 61 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Instrumented Tier0 | 527 |
| baseline | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Instrumented Tier0 | 210 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 896 |
| baseline | `ExecutionContext:get_ParentContext` | Tier1 | 39 |
| baseline | `ExecutionContext:TryGetLexicalClassCell` | Tier1 | 1,091 |
| baseline | `PyFunctionBinding:EnterInvocationFrame` | Tier1 | 1,415 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,883 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 3,886 |
| baseline | `PyFunctionBase:EnterInvocationFrame` | Tier1 | 162 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,467 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `ExecutionContext:get_ParentContext` | Tier0 | 61 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Tier0 | 184 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,910 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutionContext:get_ParentContext` | Instrumented Tier0 | 61 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Instrumented Tier0 | 210 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Instrumented Tier0 | 1,042 |
| candidate | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `ExecutionContext:get_ParentContext` | Tier1 | 39 |
| candidate | `PyFunctionBinding:EnterInvocationFrame` | Tier1 | 765 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 3,884 |
| candidate | `PyFunctionBase:EnterInvocationFrame` | Tier1 | 162 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,482 |

Final emitted Tier1 main Execute changes 3,886 to 3,884 bytes (-2), with 32 PGO / 80 single-block / 12 other inlinees in both. PyFunctionBinding.EnterInvocationFrame falls 1,415 to 765 bytes (-650), with 9 / 36 / 0 versus 2 / 29 / 0 inlinees. ParentContext getter remains 39; PyFunctionBase entry and PyExecutableFunction body remain 162 and 196. Simple-return grows 4,467 to 4,482 (+15), with 53 / 188 / 13 versus 54 / 189 / 13 inlinees. Lexical-class-cell lookup emits a 1,091-byte Tier1 baseline body and no candidate body in this selected capture; absence is not a general zero-cost or dynamic call-count proof. Early main OSR remains 8,170; later baseline OSR bodies 3,886/3,883 differ from candidate 3,910. All emitted tiers, inline summaries and call targets stay retained. Sizes do not establish total resident code-cache costs or CPU causality. The complete-job keyword gains support this tradeoff; control and allocation variations are not explained.

## Audit and cleanup

The independent audit recalculates all **517 normal responses**,
request IDs/counts, medians/IQRs, hashes/goldens, frozen producers and ordinary
worker defaults. It checks gate completion before declaration and journal ordering
through the separate diagnostics. All **18 owned services** are terminal,
**58 recorded PIDs** absent, both producers/helper rehashed and the shared VM
lease freshly free. No observation is discarded or recollected.

Raw declarations, tests, journals, native listings and cleanup proof stay private
under `.git/agent-notes/module-super-discovery-20261010/`.
Maintained [evidence](evidence.json) carries audited observations and supporting
hashes. Further runtime work, original CSV/pipeline causes, external Utf8Regex
integration and milestone/secondary qualification remain pending.
