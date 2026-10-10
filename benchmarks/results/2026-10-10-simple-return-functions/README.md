# Simple compiled function returns

Integrated: the simple-return evaluator cuts complete positional-call time 16.6–18.1% and keyword-call time 13.0–20.1% in both short replicas, with about 53% less managed job allocation. Entry/binding, checkpoints, funding and Python fallback semantics remain covered. All controls remain visible, including an unexplained first-loop increase of 7.61% that does not repeat (-0.29% in replica two). No loop/sort/pipeline benefit or qualified Python multiplier is claimed. All native bodies/tiers and the measured code growth are retained.

Twelve paired micros stop in **8.04–8.05 seconds**, including
cleanup. Separate ordinary allocation groups stop in **4.03 / 4.03 seconds**;
native-code groups in **4.03 / 4.04 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification waits for an infrequent, declared milestone.

## Frozen boundary and correctness

Baseline `096e33bdb4b2be7ab6111000b7e3845a67517c99` / production `6bc4cc2110d9e02d3e110cb77e6c7eb6cfc737af` is identical
in production to delivered green `99886f22`; its prepared Release inputs are
rehashed and reused. Candidate `1304ae71868d25af130d8e145ac058b6d59dacc6` / production
`7f211e6d82de3c931e65e7a6323657a934bf3cec` has tests `64d189a6cef54896554115d896f497ab4b60703e` and unchanged
whole benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

The compiler classifies single-block function bodies returning None, a parameter,
a constant, or an addition of two local/constant operands. The addition path
requires both runtime operands to be exact BigInteger values. Captures, closures,
exception regions, local mirroring and observable frame state use the full
interpreter. Unbound locals and additions involving rich, float or bool operands
also use that path.

Eligible invocations retain the real function context, normal argument binder,
receiver/class-cell handling, recursion/headroom checks and interpreter-depth
entry/rollback. They avoid the transient interpreter, locals array and operand
array. Every original instruction keeps its checkpoint and span. Constant loads
keep observation; addition keeps the original arbitrary-precision numeric and
ownership/funding operation and result observation. Operation exceptions retain
active-exception chaining/restoration; checkpoint errors stay outside operation
routing. Async entry uses the same ordinary synchronous function body as before.

Independent regressions `2c3d53cd` / `cc3c72e2` add 21 fuel, memory-denial,
exception-context, identity and observable/rich fallback checks. An initial fixture
used an unsupported int subclass (20 pass/1 fail); its receipt remains archived.
The fixture was corrected to a supported user class before runtime changes, and
all 21 pass on original production first. The corrected versioned baseline
assembly and receipts are archived. The frozen VM baseline producer predates
these tests but has identical baseline production. Compile-only callee inspection
uses the verified frozen baseline library and exact workload source hashes;
static shapes do not establish dynamic frequencies.
Full frozen Debug passes **9,232 checks (1,475 white / 7,757 public)**; matching
VM Release passes **204 white / 296 public**, followed by six complete canonical
CPython cases, all before normal timing declaration. Existing exact iteration,
funding/cancellation, retained/dropped ownership, generic/user protocols,
generator/closure, reentrant calls and async host suites remain in the gates.

## Fresh accepted-runtime call CPU leads

Before the candidate, two separately declared ten-second positional/keyword
captures reuse the accepted baseline producer and frozen parser without rebuilds.
Their whole groups stop in **14.07 / 16.09 seconds**, under **30-second caps**;
all **1,152 responses / 36,680 invocations** match complete output. They retain
**966 / 1,201 samples**, zero reported losses/missing stacks and **6.21% / 7.08%
unresolved leaves**. Interpreter Execute is 16.05% / 14.07% exclusive; full
guest-frame execution is 69.98% / 72.27% inclusive. Allocation ticks lead with
interpreter/context objects and object arrays. These are diagnostic leads,
not removable shares or predicted gains. Inclusive stacks overlap; profiler,
kernel and background JIT costs remain visible. Allocation ticks are sampled,
not exact bytes by guest type. GC pairs are incomplete, so no complete GC counts
or pauses are claimed. Inputs/helpers rehash; two services terminal, six PIDs
absent and the VM lease freshly free. There is no before/after CPU claim.

## Complete-job timing

Microseconds per invocation; parentheses are IQR/median. Negative change means
faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 24.215 (4.0%) | 24.415 (1.1%) | 1.515 (1.0%) | +0.83% |
| Integer loop / 1 | 1073.388 (0.9%) | 1155.123 (1.1%) | 577.673 (0.1%) | +7.61% |
| Positional calls / 1 | 532.916 (1.7%) | 444.656 (0.7%) | 121.682 (1.2%) | -16.56% |
| Keyword calls / 1 | 616.587 (1.2%) | 536.649 (0.7%) | 143.395 (0.2%) | -12.96% |
| Full stable sort + output / 1 | 2773.543 (11.9%) | 2891.980 (4.7%) | 304.566 (2.6%) | +4.27% |
| ASCII pipeline / 1 | 139.480 (4.1%) | 139.355 (1.4%) | 41.092 (0.4%) | -0.09% |
| Empty / 2 | 23.300 (5.7%) | 23.430 (3.3%) | 1.525 (0.8%) | +0.55% |
| Integer loop / 2 | 1067.951 (0.7%) | 1064.822 (1.0%) | 574.725 (2.1%) | -0.29% |
| Positional calls / 2 | 559.994 (0.6%) | 458.448 (1.1%) | 129.010 (0.3%) | -18.13% |
| Keyword calls / 2 | 624.316 (1.1%) | 498.530 (1.4%) | 137.032 (0.4%) | -20.15% |
| Full stable sort + output / 2 | 2858.817 (6.0%) | 2686.367 (3.8%) | 304.861 (0.6%) | -6.03% |
| ASCII pipeline / 2 | 138.412 (2.5%) | 139.030 (3.7%) | 41.893 (0.6%) | +0.45% |

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
| Integer loop / 1 | 1,079,432.64 | 1,079,432.64 | +0.00 |
| Positional calls / 1 | 1,146,362.48 | 540,144.96 | +606,217.52 |
| Keyword calls / 1 | 1,163,481.20 | 540,381.28 | +623,099.92 |
| Full stable sort + output / 1 | 3,534,276.24 | 3,508,840.64 | +25,435.60 |
| ASCII pipeline / 1 | 461,808.00 | 461,808.00 | +0.00 |
| Empty / 2 | 29,232.00 | 29,232.00 | +0.00 |
| Integer loop / 2 | 1,079,219.68 | 1,079,710.64 | -490.96 |
| Positional calls / 2 | 1,146,145.52 | 539,949.28 | +606,196.24 |
| Keyword calls / 2 | 1,162,821.20 | 540,213.28 | +622,607.92 |
| Full stable sort + output / 2 | 3,437,128.88 | 3,437,111.92 | +16.96 |
| ASCII pipeline / 2 | 461,808.00 | 461,808.00 | +0.00 |

## Separate native-code review

Two groups follow all ordinary timing and allocation. Only JIT disassembly flags
change; tiering/GC remain ordinary. All 24 helper rows verify complete outputs;
their allocation values are retained separately from ordinary allocation estimates.
The dump selects Execute, simple-return/operand helpers and ExecuteBody in Lokad.Lython.
All emitted bodies and tiers remain visible;
missing tiers are not inferred. Inline summaries/call targets remain in evidence.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier0 | 155 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,886 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,810 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 2,830 |
| baseline | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 185 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 2,696 |
| baseline | `PyExecutableFunction:ExecuteBody` | Tier1 | 2,789 |
| baseline | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier0 | 365 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier0 | 248 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `LythonRuntime:TryReadSimpleOperand` | Tier0 | 259 |
| candidate | `LythonRuntime:LoadSimpleOperand` | Tier0 | 166 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,906 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Instrumented Tier0 | 379 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,891 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,886 |
| candidate | `LythonRuntime:TryReadSimpleOperand` | Instrumented Tier0 | 344 |
| candidate | `LythonRuntime:LoadSimpleOperand` | Instrumented Tier0 | 196 |
| candidate | `PyExecutableFunction:ExecuteBody` | Instrumented Tier0 | 293 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| candidate | `LythonRuntime:TryReadSimpleOperand` | Tier1 | 584 |
| candidate | `LythonRuntime:LoadSimpleOperand` | Tier1 | 106 |
| candidate | `PyExecutableFunction:ExecuteBody` | Tier1 | 196 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,035 |
| candidate | `ExecutionThreads+WorkItem:Execute` | Tier1 | 225 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 3,903 |

Final emitted Tier1 Frame.Execute changes 2,696 to 3,903 bytes (+1,207 / 44.77%), with inline summaries 19 PGO / 55 single-block / 5 other versus 32 / 80 / 12. The source of its loop body is unchanged; callee execution now takes a different path, and these observed profiles differ. PyExecutableFunction ExecuteBody changes from 2,789 bytes (11 / 97 / 2 inlinees) to 196 bytes; TryExecuteSimpleReturn separately emits 4,035 (42 / 182 / 9), TryReadSimpleOperand 584 (12 / 15 / 4), and LoadSimpleOperand 106 (0 / 6 / 0). Early main OSR stays 8,170; baseline later bodies 3,886/2,810/2,830 contrast with candidate 3,906/3,891/3,886. Every emitted body/tier, inline summary and call target is retained. Do not add/subtract them to claim total resident code-cache cost, dynamic call frequency or CPU causality. The accepted growth is a measured tradeoff for repeated whole-call gains; the isolated first-loop slowdown remains unexplained.

## Audit and cleanup

The independent audit recalculates all **515 normal responses**,
request IDs/counts, medians/IQRs, hashes/goldens, frozen producers and ordinary
worker defaults. It checks gate completion before declaration and journal ordering
through the separate diagnostics. All **18 owned services** are terminal,
**58 recorded PIDs** absent, both producers/helper rehashed and the shared VM
lease freshly free. No observation is discarded or recollected.

Raw declarations, tests, journals, native listings and cleanup proof stay private
under `.git/agent-notes/simple-return-functions-20261010/`.
Maintained [evidence](evidence.json) carries audited observations and supporting
hashes. Further runtime work, original CSV/pipeline causes, external Utf8Regex
integration and milestone/secondary qualification remain pending.
