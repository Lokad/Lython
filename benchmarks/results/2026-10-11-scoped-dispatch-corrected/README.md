# Corrected function-scoped synchronous lowered dispatch

**Integrated.** Complete method jobs change **-4.44% / -10.10%**
against a shared-dispatch baseline carrying the same safety corrections. Method gains exceed the frozen max(2%, retained
spread) bound in both passes: **True**. Repeated control timing
regressions beyond that bound: **none**; repeated complete-job allocation growth above
0.5%: **none**. Both passes and all controls are retained.

Fourteen paired micros take **8.04–8.05 seconds** each,
including cleanup. Separate allocation groups take
**4.03–4.05s**, and
native-code groups **4.03–4.04s**.
Every collection has a **30-second external process-group cap**. **No full lanes
or recollection**; longer qualification remains an infrequent declared milestone.

## Source and correctness

Fresh baseline `1bf1e24130f587d45d6ea17b80f2a7fc8401d7e8` has production `e1f66e2a1abf3127863a67b1b785b80f3753af5a`;
candidate `d10564e7e2e35a6ad9ddc1788548f123f3178ef3` has production `5acf16ebcbc079b5f164c9c7cbb73deddfeb732c`.
Both share tests `1975c0fe4e44f551adb933759413911dc1fd5dbc` and whole benchmark project
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. Both Release producers were built fresh with matching probes.
Delivered production
is `5acf16ebcbc079b5f164c9c7cbb73deddfeb732c`.

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
Baseline and candidate each pass full Windows Debug **9,399 tests** and full
Linux Release **9,399 tests** (1,574 white / 7,825 public), with matching probes,
followed by seven complete canonical CPython cases each. Both producers contain
the same Unix stack-publication and exception-propagation fixes and identical tests.
Durable successful Debug binaries remain outside build directories.

The original [scoped-dispatch assessment](../2026-10-11-function-scoped-dispatch/README.md)
passed its micro rule but failed Ubuntu CI at head `50cb26e6`. A concurrent cold
stack diagnostic returned permanently unknown capacity on 31 of 32 readers:
initialization was published before its native delegates were ready. Commit
`4be9971b` fixes this publication race and adds a cold-process regression. The
Linux hook chain still crashed after that fix, including a discarded 1 MiB
reserve trial. A host-mediated callback measured about 14 MiB still available
before the overflow, implicating error propagation. Commit `d10564e7` propagates
lowered errors with ExceptionDispatchInfo after leaving catch handlers; the
previously crashing Linux tests then passed. Depth caps and the 96 KiB reserve
remain unchanged. New isolated checks retain 301 guest frames, error identity,
finally order and subsequent calls. A first test-fixture attempt used incorrect
probe JSON field names; those failures and its binary are retained separately.
The [safety evidence](safety-evidence.json) retains the diagnostic outputs and
receipt hashes, including the failed delivery's nine authenticated requests
(one recorded ANSI-format log retry). No same-head workflow rerun was used.

The preparation observer retained an old three-minute wait despite the declared
600-second correctness deadline. It interrupted the first baseline public suite.
The successful white suite was reused; only the interrupted public suite resumed,
with the remaining original preparation budget. Every attempt and receipt is
retained. This recovery precedes timing and does not recollect performance results.
The interrupted unit's failed state is retained with no remaining process;
all successful collection units are inactive. An offline audit amendment records
this distinction without changing the timing scripts or acceptance rule.

Policy and decision code were frozen before timing. Method improvement must
exceed max(2%, retained IQR/median) in both passes, with no repeated control timing
regression beyond that bound and no repeated allocation growth above 0.5%.
There is no allocation-saving floor for this CPU/call-preparation experiment.

## Complete-job timing

Microseconds per invocation; parentheses give IQR/median. Negative change means
faster than baseline. No entry/control cost is subtracted.

| Case / pass | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.210 (5.7%) | 22.777 (4.5%) | 1.480 (0.4%) | -1.86% |
| Integer loop / 1 | 1055.462 (0.9%) | 1046.732 (0.6%) | 589.407 (0.5%) | -0.83% |
| Positional calls / 1 | 424.919 (1.1%) | 426.681 (0.9%) | 122.165 (0.3%) | +0.41% |
| Keyword calls / 1 | 468.212 (1.3%) | 462.229 (1.5%) | 137.566 (0.8%) | -1.28% |
| Owned method calls / 1 | 1246.626 (0.9%) | 1191.299 (1.4%) | 114.681 (0.3%) | -4.44% |
| Full stable sort + output / 1 | 2687.654 (20.6%) | 2560.684 (6.5%) | 300.992 (0.2%) | -4.72% |
| ASCII pipeline / 1 | 138.822 (1.7%) | 138.801 (3.2%) | 41.274 (0.4%) | -0.02% |
| Empty / 2 | 24.075 (7.5%) | 23.680 (4.9%) | 1.489 (0.3%) | -1.64% |
| Integer loop / 2 | 1104.200 (1.6%) | 1106.221 (0.9%) | 576.746 (0.2%) | +0.18% |
| Positional calls / 2 | 417.535 (0.9%) | 413.550 (1.1%) | 124.738 (0.2%) | -0.95% |
| Keyword calls / 2 | 470.565 (0.3%) | 458.473 (1.1%) | 135.468 (0.5%) | -2.57% |
| Owned method calls / 2 | 1281.015 (0.3%) | 1151.584 (1.4%) | 117.902 (0.3%) | -10.10% |
| Full stable sort + output / 2 | 2700.276 (9.4%) | 2600.543 (3.8%) | 304.001 (0.4%) | -3.69% |
| ASCII pipeline / 2 | 140.094 (1.3%) | 138.946 (3.4%) | 41.351 (0.2%) | -0.82% |

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
Method savings are **24,919.68 / 82,059.52
bytes per complete job (1.34% / 4.43%)**.
These process-wide observations do not establish exact per-type counts/bytes,
independent sessions, or a constant saving across warmup and tier transitions.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,392.00 | 29,392.00 | +0.00 |
| Integer loop / 1 | 1,079,424.40 | 1,079,424.64 | -0.24 |
| Positional calls / 1 | 523,753.20 | 523,752.96 | +0.24 |
| Keyword calls / 1 | 524,015.92 | 524,015.92 | +0.00 |
| Owned method calls / 1 | 1,853,784.96 | 1,828,865.28 | +24,919.68 |
| Full stable sort + output / 1 | 3,506,012.08 | 3,508,601.20 | -2,589.12 |
| ASCII pipeline / 1 | 461,800.00 | 461,800.00 | +0.00 |
| Empty / 2 | 29,224.00 | 29,224.00 | +0.00 |
| Integer loop / 2 | 1,079,210.64 | 1,079,210.64 | +0.00 |
| Positional calls / 2 | 523,660.48 | 523,634.88 | +25.60 |
| Keyword calls / 2 | 523,950.96 | 523,847.92 | +103.04 |
| Owned method calls / 2 | 1,853,112.64 | 1,771,053.12 | +82,059.52 |
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
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,688 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Tier0 | 272 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier0 | 11,723 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier0 | 6,604 |
| baseline | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 890 |
| baseline | `PyFunction:ExecuteBody` | Tier0 | 324 |
| baseline | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,589 |
| baseline | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier0 | 48 |
| baseline | `LythonRuntime:SetObjectInstanceAttribute` | Tier0 | 161 |
| baseline | `LythonRuntime:StoreObjectInstanceAttribute` | Tier0 | 221 |
| baseline | `PyAttributeLookup:TryResolveInstanceMember` | Tier0 | 1,067 |
| baseline | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier0 | 48 |
| baseline | `LythonRuntime:GetObjectInstanceAttribute` | Tier0 | 106 |
| baseline | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier0 | 478 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,657 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Instrumented Tier0 | 284 |
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
| baseline | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 890 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Instrumented Tier0 | 8,156 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Tier1 | 900 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier1 | 12,023 |
| baseline | `PyAttributeLookup:TryResolveInstanceMember` | Tier1 | 4,700 |
| baseline | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier1 | 35 |
| baseline | `LythonRuntime:GetObjectInstanceAttribute` | Tier1 | 108 |
| baseline | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier1 | 3,399 |
| baseline | `PyMemberAccess:TryAssign` | Tier1 | 6,277 |
| baseline | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier1 | 35 |
| baseline | `LythonRuntime:SetObjectInstanceAttribute` | Tier1 | 3,234 |
| baseline | `LythonRuntime:StoreObjectInstanceAttribute` | Tier1 | 2,251 |
| baseline | `PyFunction:ExecuteBody` | Tier1 | 201 |
| baseline | `LythonRuntime:ExecuteStatements` | Tier1 | 2,208 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier1 | 6,831 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 3,898 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,467 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,907 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Tier0 | 334 |
| candidate | `ExecutionContext:get_UseSynchronousLoweredFunctionDispatch` | Tier0 | 28 |
| candidate | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier0 | 11,723 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier0 | 6,604 |
| candidate | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 984 |
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
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Instrumented Tier0 | 337 |
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
| candidate | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 984 |
| candidate | `LythonRuntime:DispatchLoweredStatement` | Instrumented Tier0 | 2,664 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Instrumented Tier0 | 8,156 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Tier1 | 458 |
| candidate | `ExecutionContext:get_UseSynchronousLoweredFunctionDispatch` | Tier1 | 5 |
| candidate | `LythonRuntime:DispatchLoweredExpression` | Tier1 | 2,515 |
| candidate | `ExecutionContext:set_UseSynchronousLoweredFunctionDispatch` | Tier1 | 5 |
| candidate | `PyAttributeLookup:TryResolveInstanceMember` | Tier1 | 4,690 |
| candidate | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier1 | 35 |
| candidate | `LythonRuntime:GetObjectInstanceAttribute` | Tier1 | 108 |
| candidate | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier1 | 3,380 |
| candidate | `PyMemberAccess:TryAssign` | Tier1 | 6,305 |
| candidate | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier1 | 35 |
| candidate | `LythonRuntime:SetObjectInstanceAttribute` | Tier1 | 3,503 |
| candidate | `LythonRuntime:StoreObjectInstanceAttribute` | Tier1 | 2,251 |
| candidate | `PyFunction:ExecuteBody` | Tier1 | 236 |
| candidate | `LythonRuntime:ExecuteStatements` | Tier1 | 847 |
| candidate | `LythonRuntime:DispatchLoweredStatement` | Tier1 | 1,247 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 5,122 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier1 | 6,785 |
| candidate | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier1 | 12,190 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,467 |

Native bodies and inline/call-target changes describe emitted code. They do not
establish a resident code-cache total or causal CPU attribution. No new CPU trace
or qualified before/after CPU share is claimed in this round.

## Audit and next work

Independent audits recalculate **602 normal responses**, sequences,
full output hashes, medians/IQRs and identities. Correctness gates precede timing;
allocation/native order and unchanged inputs are verified. **21 owned services**
are terminal, **67 recorded PIDs** absent, both producers/helper rehashed, and the
shared VM lease freshly available. Raw receipts, declarations, listings and journals
stay private under `.git/agent-notes/scoped-dispatch-corrected-20261011/`.

The earlier global direct-dispatch, method receiver lease and unused-super-
anchor decisions remain rejected. The default-object slot change remains accepted. Eligible executable class-method bodies remain
further design work. Original CSV/pipeline/C05 causes, matched milestone/secondary
qualification, the additional call-result assignment/property-constructor findings
and external Utf8Regex integration remain open.
