# Exact default object attribute slots

**Integrated.** Complete method jobs change **-31.03% / -28.27%**
against the corrected baseline. Method gains exceed the frozen max(2%, retained
spread) bound in both passes: **True**. Repeated control timing
regressions beyond that bound: **none**; repeated complete-job allocation growth above
0.5%: **none**. Both passes and all controls are retained.

Fourteen paired micros take **8.04–8.06 seconds** each,
including cleanup. Separate allocation groups take
**4.03–4.04s**, and
native-code groups **4.03–4.04s**.
Every collection has a **30-second external process-group cap**. **No full lanes
or recollection**; longer qualification remains an infrequent declared milestone.

## Source and correctness

Fresh baseline `fabf2e24539953fa73f219f89e337a0fe565852b` has production `ea4886034b01717ee5b83ceb0207a4b974adff86`;
candidate `122384e3099cf25fd162694d9273abbce1f2fed5` has production `d5165bbb6cb51aac155234a30e4df91ff8011b96`.
Both share tests `0a463a1c1aad66197fb5563784a4bd1bd9a70057` and whole benchmark project
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. Both Release producers were built fresh. Older prepared
manifests are not identical after the compatibility fixes. Delivered production
is `d5165bbb6cb51aac155234a30e4df91ff8011b96`.

The candidate recognizes only the sealed engine default get/set attribute slots
after each live MRO lookup. Those slots route directly through shared existing
descriptor-aware handlers. Custom overrides, slot descriptors and explicit
object-slot calls retain their contracts. The shared handlers preserve actual
contexts/services/source, descriptor precedence, missing-attribute fallback,
checkpoint ordering, logical fees, owned attribute growth and error spans.
Asynchronous handlers still await genuine descriptor/host suspension. No method
factory, loop dispatcher or delete fast path changes in this experiment.

The [preceding compatibility report](../2026-10-10-default-object-attribute-baseline/README.md)
retains all **33 independent checks**, **nine corrected-fixture failures** on
original production, initial fixture/build mistakes and their corrections.
The corrected baseline and candidate each pass **154 selected checks**, full
Debug **9,368 tests** (1,547 white / 7,821 public) and **58 CPython comparisons**,
with matching probes built. Fresh VM Release gates pass **276 white / 390 public
checks for each producer**, followed by seven complete canonical CPython cases.
The benchmark sources, fixtures and complete expected outputs are unchanged.

Policy and decision code were frozen before timing. Method improvement must
exceed max(2%, retained IQR/median) in both passes, with no repeated control timing
regression beyond that bound and no repeated allocation growth above 0.5%.
There is no allocation-saving floor for this CPU/call-preparation experiment.
The receipt count was corrected to 58 before timing with thresholds and code
unchanged. An offline preparation-count check initially mixed the test assemblies;
it was corrected before declarations or VM work, without timing recollection.

## Complete-job timing

Microseconds per invocation; parentheses give IQR/median. Negative change means
faster than baseline. No entry/control cost is subtracted.

| Case / pass | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.401 (2.9%) | 23.444 (2.3%) | 1.506 (0.3%) | +0.18% |
| Integer loop / 1 | 1119.596 (0.2%) | 1060.605 (0.8%) | 631.355 (1.3%) | -5.27% |
| Positional calls / 1 | 419.803 (0.8%) | 438.236 (1.1%) | 122.371 (0.7%) | +4.39% |
| Keyword calls / 1 | 463.920 (1.4%) | 465.884 (0.5%) | 135.912 (0.5%) | +0.42% |
| Owned method calls / 1 | 1778.938 (0.9%) | 1226.938 (1.2%) | 117.932 (0.2%) | -31.03% |
| Full stable sort + output / 1 | 2672.232 (12.1%) | 2849.147 (5.2%) | 303.197 (0.4%) | +6.62% |
| ASCII pipeline / 1 | 139.964 (0.6%) | 137.159 (2.4%) | 41.335 (0.3%) | -2.00% |
| Empty / 2 | 23.194 (4.9%) | 23.447 (5.2%) | 1.478 (0.5%) | +1.09% |
| Integer loop / 2 | 1051.727 (0.4%) | 1043.851 (0.8%) | 617.761 (7.9%) | -0.75% |
| Positional calls / 2 | 436.948 (1.7%) | 420.900 (1.2%) | 120.424 (0.4%) | -3.67% |
| Keyword calls / 2 | 468.988 (1.2%) | 466.357 (1.6%) | 135.257 (0.4%) | -0.56% |
| Owned method calls / 2 | 1808.972 (0.8%) | 1297.604 (2.0%) | 115.515 (0.6%) | -28.27% |
| Full stable sort + output / 2 | 2681.090 (18.9%) | 2763.904 (5.9%) | 303.585 (0.2%) | +3.09% |
| ASCII pipeline / 2 | 145.238 (3.3%) | 138.297 (2.5%) | 41.457 (1.0%) | -4.78% |

Full sort/output changes +6.62% / +3.09%, within the retained 12.1% / 18.9%
maximum spreads. Positional calls change +4.39% / -3.67%. These controls therefore
do not fail the prospective repeated-regression rule; their results do not prove
equal or improved cost. The method gain exceeds its 1.2% / 2.0% maximum spreads.

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
Method savings are **1,737,254.48 / 1,737,165.36
bytes per complete job (48.38% / 48.39%)**.
These process-wide observations do not establish exact per-type counts/bytes,
independent sessions, or a constant saving across warmup and tier transitions.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,392.00 | 29,392.00 | +0.00 |
| Integer loop / 1 | 1,079,424.64 | 1,079,424.40 | +0.24 |
| Positional calls / 1 | 523,752.96 | 523,752.96 | +0.00 |
| Keyword calls / 1 | 524,015.92 | 524,016.16 | -0.24 |
| Owned method calls / 1 | 3,591,102.16 | 1,853,847.68 | +1,737,254.48 |
| Full stable sort + output / 1 | 3,509,951.60 | 3,520,460.64 | -10,509.04 |
| ASCII pipeline / 1 | 461,800.00 | 461,800.00 | +0.00 |
| Empty / 2 | 29,224.00 | 29,224.00 | +0.00 |
| Integer loop / 2 | 1,079,210.64 | 1,079,210.64 | +0.00 |
| Positional calls / 2 | 523,687.52 | 523,660.80 | +26.72 |
| Keyword calls / 2 | 523,895.92 | 523,951.84 | -55.92 |
| Owned method calls / 2 | 3,590,287.84 | 1,853,122.48 | +1,737,165.36 |
| Full stable sort + output / 2 | 3,420,711.92 | 3,420,711.92 | +0.00 |
| ASCII pipeline / 2 | 461,800.00 | 461,800.00 | +0.00 |

## Separate native-code review

Both native groups follow all ordinary timing and allocation. Only JIT dump flags
change; ordinary tiering/GC remains. All 28 helper rows check complete output.
Their allocation rows stay separate from the ordinary allocation estimates.
The filter covers exact-slot guards/shared handlers, attribute lookup/assignment,
library MoveNext bodies, invocation/binder/lease helpers and executable/lowered
entry points. All emitted filtered bodies, tiers, inline summaries and call targets
remain in [evidence](evidence.json); this table selects the experiment's boundaries.
An absent emitted body or tier does not establish zero cost or prove inlining.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,907 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Tier0 | 232 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier0 | 11,723 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier0 | 6,604 |
| baseline | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 822 |
| baseline | `PyFunction:ExecuteBody` | Tier0 | 324 |
| baseline | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,529 |
| baseline | `ObjectSetAttrMethod:Invoke` | Tier0 | 1,169 |
| baseline | `PyAttributeLookup:TryResolveInstanceMember` | Tier0 | 912 |
| baseline | `ObjectGetAttrMethod:Invoke` | Tier0 | 624 |
| baseline | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier0 | 478 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,655 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Instrumented Tier0 | 203 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Instrumented Tier0 | 14,340 |
| baseline | `PyAttributeLookup:TryResolveInstanceMember` | Instrumented Tier0 | 1,077 |
| baseline | `ObjectGetAttrMethod:Invoke` | Instrumented Tier0 | 718 |
| baseline | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Instrumented Tier0 | 657 |
| baseline | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,529 |
| baseline | `ObjectSetAttrMethod:Invoke` | Instrumented Tier0 | 1,335 |
| baseline | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| baseline | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 822 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Instrumented Tier0 | 8,156 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Tier1 | 864 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier1 | 12,013 |
| baseline | `PyAttributeLookup:TryResolveInstanceMember` | Tier1 | 3,217 |
| baseline | `ObjectGetAttrMethod:Invoke` | Tier1 | 4,037 |
| baseline | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier1 | 3,379 |
| baseline | `PyMemberAccess:TryAssign` | Tier1 | 4,827 |
| baseline | `ObjectSetAttrMethod:Invoke` | Tier1 | 4,499 |
| baseline | `PyFunction:ExecuteBody` | Tier1 | 201 |
| baseline | `LythonRuntime:ExecuteStatements` | Tier1 | 2,162 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier1 | 6,831 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 4,957 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,498 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,823 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Tier0 | 233 |
| candidate | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier0 | 11,723 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier0 | 6,604 |
| candidate | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 822 |
| candidate | `PyFunction:ExecuteBody` | Tier0 | 324 |
| candidate | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,589 |
| candidate | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier0 | 48 |
| candidate | `LythonRuntime:SetObjectInstanceAttribute` | Tier0 | 161 |
| candidate | `LythonRuntime:StoreObjectInstanceAttribute` | Tier0 | 221 |
| candidate | `PyAttributeLookup:TryResolveInstanceMember` | Tier0 | 1,067 |
| candidate | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier0 | 48 |
| candidate | `LythonRuntime:GetObjectInstanceAttribute` | Tier0 | 106 |
| candidate | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier0 | 478 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 5,117 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Instrumented Tier0 | 203 |
| candidate | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Instrumented Tier0 | 14,340 |
| candidate | `PyAttributeLookup:TryResolveInstanceMember` | Instrumented Tier0 | 1,271 |
| candidate | `LythonRuntime:IsDefaultObjectGetAttribute` | Instrumented Tier0 | 48 |
| candidate | `LythonRuntime:GetObjectInstanceAttribute` | Instrumented Tier0 | 106 |
| candidate | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Instrumented Tier0 | 657 |
| candidate | `PyMemberAccess:TryAssign` | Instrumented Tier0 | 4,588 |
| candidate | `LythonRuntime:IsDefaultObjectSetAttribute` | Instrumented Tier0 | 48 |
| candidate | `LythonRuntime:SetObjectInstanceAttribute` | Instrumented Tier0 | 207 |
| candidate | `LythonRuntime:StoreObjectInstanceAttribute` | Instrumented Tier0 | 251 |
| candidate | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| candidate | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 822 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Instrumented Tier0 | 8,156 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Tier1 | 861 |
| candidate | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier1 | 12,013 |
| candidate | `PyAttributeLookup:TryResolveInstanceMember` | Tier1 | 4,700 |
| candidate | `LythonRuntime:IsDefaultObjectGetAttribute` | Tier1 | 35 |
| candidate | `LythonRuntime:GetObjectInstanceAttribute` | Tier1 | 108 |
| candidate | `PyAttributeLookup:TryResolveInstanceMemberWithoutGetAttrFallback` | Tier1 | 3,399 |
| candidate | `PyMemberAccess:TryAssign` | Tier1 | 6,277 |
| candidate | `LythonRuntime:IsDefaultObjectSetAttribute` | Tier1 | 35 |
| candidate | `LythonRuntime:SetObjectInstanceAttribute` | Tier1 | 3,234 |
| candidate | `LythonRuntime:StoreObjectInstanceAttribute` | Tier1 | 2,251 |
| candidate | `PyFunction:ExecuteBody` | Tier1 | 201 |
| candidate | `LythonRuntime:ExecuteStatements` | Tier1 | 2,162 |
| candidate | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier1 | 6,831 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 5,150 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,838 |

Native bodies and inline/call-target changes describe emitted code. They do not
establish a resident code-cache total or causal CPU attribution. No new CPU trace
or qualified before/after CPU share is claimed in this round.

## Audit and next work

Independent audits recalculate **600 normal responses**, sequences,
full output hashes, medians/IQRs and identities. Correctness gates precede timing;
allocation/native order and unchanged inputs are verified. **20 owned services**
are terminal, **66 recorded PIDs** absent, both producers/helper rehashed, and the
shared VM lease freshly available. Raw receipts, declarations, listings and journals
stay private under `.git/agent-notes/default-attribute-micros-20261010/`.

The prior direct synchronous dispatch, method receiver lease and unused-super-
anchor decisions remain rejected. Eligible executable class-method bodies remain
further design work. Original CSV/pipeline/C05 causes, matched milestone/secondary
qualification, the additional call-result assignment/property-constructor findings
and external Utf8Regex integration remain open.
