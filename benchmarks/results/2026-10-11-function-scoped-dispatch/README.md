# Function-scoped synchronous lowered dispatch

**Integrated.** Complete method jobs change **-3.63% / -8.33%**
against the accepted default-slot baseline. Method gains exceed the frozen max(2%, retained
spread) bound in both passes: **True**. Repeated control timing
regressions beyond that bound: **none**; repeated complete-job allocation growth above
0.5%: **none**. Both passes and all controls are retained.

Fourteen paired micros take **8.04–8.06 seconds** each,
including cleanup. Separate allocation groups take
**4.03–4.03s**, and
native-code groups **4.03–4.03s**.
Every collection has a **30-second external process-group cap**. **No full lanes
or recollection**; longer qualification remains an infrequent declared milestone.

## Source and correctness

Fresh baseline `2fc2ad55946307cdf267a0f443053e21f534f370` has production `d5165bbb6cb51aac155234a30e4df91ff8011b96`;
candidate `0445ca571417aa2ae20bc833e8839a845e6dac52` has production `1bee4c9234fb1d56821d7b864b6339aa48380df2`.
Both share tests `07cd1ebf8666e2a4abe6fcf402d37fbd404edc78` and whole benchmark project
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. Both Release producers were built fresh with matching probes.
Delivered production
is `1bee4c9234fb1d56821d7b864b6339aa48380df2`.

Only active synchronous `PyFunction` bodies select direct statement/expression
dispatch in their actual frames. The prior strategy is restored in a finally
block. Child contexts initialize their own strategy; actual async bodies retain
the existing awaited route. Direct dispatch calls existing operations and preserves
checkpoint/source handling, logical funding, cells, argument mirroring and binder
leases. No executable fallback, function factory or object-slot change is made.

The [preceding baseline assessment](../2026-10-11-function-scoped-dispatch-baseline/README.md)
retains **27 independent invocation checks** added before the prototype, including
fuel/span/source, cancellation ordering, admission rollback and actual frame
identity/funding. Its initial fixture expectation failure and corrected original
production pass are retained; no runtime compatibility fix was inferred.
Baseline and candidate each pass **181 selected checks**, full Debug **9,395 tests**
(1,574 white / 7,821 public) and **58 CPython comparisons**, with matching probes.
Fresh VM Release gates pass **303 white / 390 public checks per producer**, followed
by seven complete canonical CPython cases. Sources, fixtures and full outputs are
unchanged. Durable successful Debug binaries remain outside build directories.

Policy and decision code were frozen before timing. Method improvement must
exceed max(2%, retained IQR/median) in both passes, with no repeated control timing
regression beyond that bound and no repeated allocation growth above 0.5%.
There is no allocation-saving floor for this CPU/call-preparation experiment.

## Complete-job timing

Microseconds per invocation; parentheses give IQR/median. Negative change means
faster than baseline. No entry/control cost is subtracted.

| Case / pass | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.546 (3.6%) | 23.513 (2.6%) | 1.508 (1.4%) | -0.14% |
| Integer loop / 1 | 1062.059 (0.7%) | 1067.308 (0.8%) | 681.176 (0.3%) | +0.49% |
| Positional calls / 1 | 445.249 (0.4%) | 419.685 (1.1%) | 120.183 (0.3%) | -5.74% |
| Keyword calls / 1 | 461.870 (1.3%) | 456.338 (0.7%) | 134.946 (0.5%) | -1.20% |
| Owned method calls / 1 | 1293.045 (1.2%) | 1246.109 (1.3%) | 114.768 (0.2%) | -3.63% |
| Full stable sort + output / 1 | 2670.181 (7.7%) | 2876.432 (7.5%) | 301.764 (0.7%) | +7.72% |
| ASCII pipeline / 1 | 139.793 (3.9%) | 136.695 (1.9%) | 41.799 (0.4%) | -2.22% |
| Empty / 2 | 23.132 (4.4%) | 23.413 (1.0%) | 1.464 (0.4%) | +1.21% |
| Integer loop / 2 | 1087.281 (0.7%) | 1036.484 (0.7%) | 664.849 (0.2%) | -4.67% |
| Positional calls / 2 | 425.482 (0.8%) | 420.446 (0.6%) | 120.154 (0.4%) | -1.18% |
| Keyword calls / 2 | 465.432 (1.3%) | 472.131 (0.5%) | 144.091 (0.2%) | +1.44% |
| Owned method calls / 2 | 1282.352 (0.4%) | 1175.533 (1.6%) | 123.406 (0.3%) | -8.33% |
| Full stable sort + output / 2 | 2686.822 (7.3%) | 2640.293 (7.0%) | 302.225 (0.3%) | -1.73% |
| ASCII pipeline / 2 | 137.541 (1.2%) | 140.329 (2.4%) | 41.455 (0.5%) | +2.03% |

Every control and spread is retained in the table. A control falling inside the
frozen spread bound does not establish equal or improved cost. The decision uses
both passes, including unfavorable observations.

Compilation precedes timing and every invocation gets fresh guest state. Workers
use SDK 10.0.401 / CLR 10.0.12, ordinary tiering/PGO, workstation GC and Interactive
latency without runtime overrides. CPython 3.13.16 uses PGO/LTO, -I -S, ordinary
GIL and GC. Entry, materialization and containment costs remain in the job.
These short paired results guide acceptance of this specific candidate; they do
not supply a milestone-qualified Python multiplier or a language-wide claim.

## Separate allocation diagnostic

The frozen exact Compile/Invoke helper is rehashed and reused without rebuilding.
GC.GetTotalAllocatedBytes(precise:true) brackets 100 invocations after 16 warmups
per case/pass; two passes share each role's process. All 28 rows check complete
output: 2,800 measured and 448 warmup invocations, without forced collection.
Method savings are **28,055.68 / 82,000.00
bytes per complete job (1.51% / 4.42%)**.
These process-wide observations do not establish exact per-type counts/bytes,
independent sessions, or a constant saving across warmup and tier transitions.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,392.00 | 29,392.00 | +0.00 |
| Integer loop / 1 | 1,079,424.64 | 1,079,424.64 | +0.00 |
| Positional calls / 1 | 523,752.96 | 523,752.96 | +0.00 |
| Keyword calls / 1 | 524,015.92 | 524,015.92 | +0.00 |
| Owned method calls / 1 | 1,853,810.24 | 1,825,754.56 | +28,055.68 |
| Full stable sort + output / 1 | 3,511,229.36 | 3,500,763.76 | +10,465.60 |
| ASCII pipeline / 1 | 461,800.00 | 461,800.00 | +0.00 |
| Empty / 2 | 29,224.00 | 29,224.00 | +0.00 |
| Integer loop / 2 | 1,079,210.64 | 1,079,210.64 | +0.00 |
| Positional calls / 2 | 523,661.12 | 523,633.92 | +27.20 |
| Keyword calls / 2 | 523,952.00 | 523,847.92 | +104.08 |
| Owned method calls / 2 | 1,853,149.12 | 1,771,149.12 | +82,000.00 |
| Full stable sort + output / 2 | 3,420,711.92 | 3,420,711.92 | +0.00 |
| ASCII pipeline / 2 | 461,800.00 | 461,800.00 | +0.00 |

## Separate native-code review

Both native groups follow all ordinary timing and allocation. Only JIT dump flags
change; ordinary tiering/GC remains. All 28 helper rows check complete output.
Their allocation rows stay separate from the ordinary allocation estimates.
The filter covers scoped strategy guards/body dispatch, shared async dispatch, attribute lookup/assignment,
library MoveNext bodies, invocation/binder/lease helpers and executable/lowered
entry points. All emitted filtered bodies, tiers, inline summaries and call targets
remain in [evidence](evidence.json); this table selects the experiment's boundaries.
An absent emitted body or tier does not establish zero cost or prove inlining.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,905 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Tier0 | 232 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier0 | 11,723 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier0 | 6,604 |
| baseline | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 822 |
| baseline | `PyFunction:ExecuteBody` | Tier0 | 324 |
| baseline | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,588 |
| baseline | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier0 | 48 |
| baseline | `LythonRuntime:SetObjectInstanceAttribute` | Tier0 | 161 |
| baseline | `LythonRuntime:StoreObjectInstanceAttribute` | Tier0 | 221 |
| baseline | `PyAttributeLookup:TryResolveInstanceMember` | Tier0 | 1,067 |
| baseline | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier0 | 48 |
| baseline | `LythonRuntime:GetObjectInstanceAttribute` | Tier0 | 106 |
| baseline | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier0 | 478 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,655 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Instrumented Tier0 | 203 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Instrumented Tier0 | 14,340 |
| baseline | `PyAttributeLookup:TryResolveInstanceMember` | Instrumented Tier0 | 1,271 |
| baseline | `LythonRuntime:IsDefaultObjectGetAttribute` | Instrumented Tier0 | 48 |
| baseline | `LythonRuntime:GetObjectInstanceAttribute` | Instrumented Tier0 | 106 |
| baseline | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Instrumented Tier0 | 657 |
| baseline | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,588 |
| baseline | `LythonRuntime:IsDefaultObjectSetAttribute` | Instrumented Tier0 | 48 |
| baseline | `LythonRuntime:SetObjectInstanceAttribute` | Instrumented Tier0 | 207 |
| baseline | `LythonRuntime:StoreObjectInstanceAttribute` | Instrumented Tier0 | 251 |
| baseline | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| baseline | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 822 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Instrumented Tier0 | 8,156 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Tier1 | 861 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier1 | 12,013 |
| baseline | `PyAttributeLookup:TryResolveInstanceMember` | Tier1 | 4,700 |
| baseline | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier1 | 35 |
| baseline | `LythonRuntime:GetObjectInstanceAttribute` | Tier1 | 108 |
| baseline | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier1 | 3,394 |
| baseline | `PyMemberAccess:TryAssign` | Tier1 | 6,263 |
| baseline | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier1 | 35 |
| baseline | `LythonRuntime:SetObjectInstanceAttribute` | Tier1 | 3,234 |
| baseline | `LythonRuntime:StoreObjectInstanceAttribute` | Tier1 | 2,251 |
| baseline | `PyFunction:ExecuteBody` | Tier1 | 201 |
| baseline | `LythonRuntime:ExecuteStatements` | Tier1 | 2,162 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier1 | 6,831 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 4,935 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,482 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,823 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Tier0 | 271 |
| candidate | `ExecutionContext:get_UseSynchronousLoweredFunctionDispatch` | Tier0 | 28 |
| candidate | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier0 | 11,723 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier0 | 6,604 |
| candidate | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 922 |
| candidate | `PyFunction:ExecuteBody` | Tier0 | 407 |
| candidate | `ExecutionContext:set_UseSynchronousLoweredFunctionDispatch` | Tier0 | 33 |
| candidate | `LythonRuntime:DispatchLoweredStatement` | Tier0 | 2,257 |
| candidate | `LythonRuntime:DispatchLoweredExpression` | Tier0 | 4,018 |
| candidate | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,589 |
| candidate | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier0 | 48 |
| candidate | `LythonRuntime:SetObjectInstanceAttribute` | Tier0 | 161 |
| candidate | `LythonRuntime:StoreObjectInstanceAttribute` | Tier0 | 221 |
| candidate | `PyAttributeLookup:TryResolveInstanceMember` | Tier0 | 1,067 |
| candidate | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier0 | 48 |
| candidate | `LythonRuntime:GetObjectInstanceAttribute` | Tier0 | 106 |
| candidate | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier0 | 478 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 5,125 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Instrumented Tier0 | 283 |
| candidate | `ExecutionContext:get_UseSynchronousLoweredFunctionDispatch` | Instrumented Tier0 | 28 |
| candidate | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Instrumented Tier0 | 14,340 |
| candidate | `LythonRuntime:DispatchLoweredExpression` | Instrumented Tier0 | 4,689 |
| candidate | `ExecutionContext:set_UseSynchronousLoweredFunctionDispatch` | Instrumented Tier0 | 33 |
| candidate | `PyAttributeLookup:TryResolveInstanceMember` | Instrumented Tier0 | 1,271 |
| candidate | `LythonRuntime:IsDefaultObjectGetAttribute` | Instrumented Tier0 | 48 |
| candidate | `LythonRuntime:GetObjectInstanceAttribute` | Instrumented Tier0 | 106 |
| candidate | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Instrumented Tier0 | 657 |
| candidate | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,588 |
| candidate | `LythonRuntime:IsDefaultObjectSetAttribute` | Instrumented Tier0 | 48 |
| candidate | `LythonRuntime:SetObjectInstanceAttribute` | Instrumented Tier0 | 207 |
| candidate | `LythonRuntime:StoreObjectInstanceAttribute` | Instrumented Tier0 | 251 |
| candidate | `PyFunction:ExecuteBody` | Instrumented Tier0 | 482 |
| candidate | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 922 |
| candidate | `LythonRuntime:DispatchLoweredStatement` | Instrumented Tier0 | 2,664 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Instrumented Tier0 | 8,156 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Tier1 | 429 |
| candidate | `ExecutionContext:get_UseSynchronousLoweredFunctionDispatch` | Tier1 | 5 |
| candidate | `LythonRuntime:DispatchLoweredExpression` | Tier1 | 2,515 |
| candidate | `ExecutionContext:set_UseSynchronousLoweredFunctionDispatch` | Tier1 | 5 |
| candidate | `PyAttributeLookup:TryResolveInstanceMember` | Tier1 | 4,923 |
| candidate | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier1 | 35 |
| candidate | `LythonRuntime:GetObjectInstanceAttribute` | Tier1 | 108 |
| candidate | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier1 | 3,380 |
| candidate | `PyMemberAccess:TryAssign` | Tier1 | 6,305 |
| candidate | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier1 | 35 |
| candidate | `LythonRuntime:SetObjectInstanceAttribute` | Tier1 | 3,503 |
| candidate | `LythonRuntime:StoreObjectInstanceAttribute` | Tier1 | 2,251 |
| candidate | `PyFunction:ExecuteBody` | Tier1 | 236 |
| candidate | `LythonRuntime:ExecuteStatements` | Tier1 | 792 |
| candidate | `LythonRuntime:DispatchLoweredStatement` | Tier1 | 1,247 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 5,124 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier1 | 6,785 |
| candidate | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier1 | 12,200 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,838 |

Native bodies and inline/call-target changes describe emitted code. They do not
establish a resident code-cache total or causal CPU attribution. No new CPU trace
or qualified before/after CPU share is claimed in this round.

## Audit and next work

Independent audits recalculate **599 normal responses**, sequences,
full output hashes, medians/IQRs and identities. Correctness gates precede timing;
allocation/native order and unchanged inputs are verified. **20 owned services**
are terminal, **66 recorded PIDs** absent, both producers/helper rehashed, and the
shared VM lease freshly available. Raw receipts, declarations, listings and journals
stay private under `.git/agent-notes/function-scoped-micros-20261011/`.

The earlier global direct-dispatch, method receiver lease and unused-super-
anchor decisions remain rejected. The default-object slot change remains accepted. Eligible executable class-method bodies remain
further design work. Original CSV/pipeline/C05 causes, matched milestone/secondary
qualification, the additional call-result assignment/property-constructor findings
and external Utf8Regex integration remain open.
