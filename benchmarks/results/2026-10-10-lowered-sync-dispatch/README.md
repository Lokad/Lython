# Direct synchronous lowered dispatch

**Rejected; the candidate stays isolated.** Method jobs improve **6.62% / 4.81%**
and keyword jobs **4.90% / 4.12%**, beyond both retained spreads. The integer-loop
control regresses **3.21% / 2.58%**, exceeding the predeclared max(2%, spread)
bound in both passes. Positional calls change +2.82% / +0.29%; full sort/output
+9.50% / -0.69% with 8.1–15.4% spread; pipeline +0.18% / +0.80%; empty
+1.46% / -0.61%. Every control and replica is retained.

Fourteen paired micros take **8.04–8.06 seconds** each,
including cleanup. Separate ordinary allocation and native-code groups take
about **4.03 seconds** each. All collections have a **30-second external
process-group cap**. **No full lanes or recollection**; longer qualification
remains an infrequent, declared milestone.

## Source and correctness

Fresh baseline `d21b7335c3778ac974adc2f381a80906af0c6a8e` has production `7fe2dd6ab6f53578d03dc94564110d97f5912840`;
isolated candidate `d1a1c599d4264fe49b29c7288a10a5bb1d755522` has production `ea808ce93c0dbae7b78ddb370459decdc91e9bdf`.
Both share tests `e058c64e74d5eaaa6549a5653dc61b690b0f440d` and whole benchmark project
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. Both producers were built fresh in Release: the older
prepared baseline cannot be treated as identical after the frontend fixes.
Delivered production remains the corrected baseline on master.

The candidate replaces shared async statement/expression dispatch with direct
synchronous methods, including executable fallback statements. It maps all 33
synchronous actions to their existing operations. Expression entry checkpoints
and source annotation, evaluation order, control signals, closures/generators,
actual contexts/services, logical funding and ownership remain covered. Genuine
async execution keeps its strategy. This candidate does not change the class
method factory, operator representation or binder.

Both baseline and candidate full Debug suites pass **9,335 tests** (1,537 white /
7,798 public), with matching probes built. The candidate also passes 121 focused
boundaries. Twenty trusted snippets match CPython in both modes for each source.
Fresh VM Release gates pass **266 white / 367 public for each producer**, and
seven canonical cases check full CPython output before timing. Test sources and
the benchmark catalog, fixtures and complete goldens remain identical.
The [preceding compatibility report](../2026-10-10-lowered-dispatch-baseline/README.md)
retains all 34 new checks, original fixture mistakes and reproduced frontend
failures; those corrections precede this experiment.

The decision policy and code were frozen before timing: method improvement must
exceed max(2%, retained IQR/median) in both passes, with no repeated control timing
regression beyond that bound and no repeated allocation growth above 0.5% in any
case. CPU dispatch has no allocation-saving floor. The target passes, the loop
control fails, and allocation growth does not fail. The policy is unchanged.

## Complete-job timing

Microseconds per invocation; parentheses give IQR/median. Negative change means
faster than baseline. Both passes and all controls remain visible.

| Case / pass | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.100 (2.9%) | 23.438 (4.7%) | 1.532 (0.8%) | +1.46% |
| Integer loop / 1 | 1051.530 (0.9%) | 1085.238 (1.0%) | 589.094 (0.1%) | +3.21% |
| Positional calls / 1 | 421.409 (0.7%) | 433.313 (1.2%) | 120.398 (0.6%) | +2.82% |
| Keyword calls / 1 | 515.014 (1.0%) | 489.753 (0.9%) | 143.526 (0.2%) | -4.90% |
| Owned method calls / 1 | 1774.149 (1.2%) | 1656.687 (1.3%) | 116.713 (0.6%) | -6.62% |
| Full stable sort + output / 1 | 2640.054 (8.1%) | 2890.743 (15.4%) | 304.119 (1.1%) | +9.50% |
| ASCII pipeline / 1 | 139.080 (2.0%) | 139.330 (2.4%) | 41.386 (0.5%) | +0.18% |
| Empty / 2 | 23.515 (4.1%) | 23.372 (2.8%) | 1.486 (1.9%) | -0.61% |
| Integer loop / 2 | 1035.942 (0.8%) | 1062.669 (0.9%) | 687.615 (0.2%) | +2.58% |
| Positional calls / 2 | 427.407 (1.1%) | 428.636 (0.8%) | 122.038 (0.5%) | +0.29% |
| Keyword calls / 2 | 479.149 (0.9%) | 459.395 (1.5%) | 140.848 (0.3%) | -4.12% |
| Owned method calls / 2 | 1790.331 (1.5%) | 1704.152 (1.3%) | 115.051 (0.2%) | -4.81% |
| Full stable sort + output / 2 | 2557.072 (7.0%) | 2539.388 (11.6%) | 308.301 (0.7%) | -0.69% |
| ASCII pipeline / 2 | 137.658 (2.2%) | 138.761 (2.2%) | 41.487 (0.3%) | +0.80% |

Compilation precedes timing and every invocation gets fresh guest state. Workers
use SDK 10.0.401 / CLR 10.0.12, ordinary tiering/PGO, workstation GC and Interactive
latency without runtime overrides. CPython 3.13.16 uses PGO/LTO, -I -S and ordinary
GIL/GC. Entry, materialization and containment costs stay inside the job; no
control time is subtracted. CPython loop medians differ 589 / 688 µs between
passes, so this round supplies no qualified Python multiplier or language-wide
claim. The short paired results guide acceptance of this specific candidate.

## Separate allocation diagnostic

The frozen exact Compile/Invoke helper is rehashed and reused without rebuilding.
GC.GetTotalAllocatedBytes(precise:true) brackets 100 invocations after 16 warmups
per case/pass; two passes share each role's process. All 28 rows verify full
output: 2,800 measured and 448 warmup invocations, with no forced collection.
Method savings are **36,326.72 / 81,920.08 bytes per complete job (1.01% / 2.28%)**.
These process-wide observations do not establish exact per-type counts/bytes,
independent sessions or a constant saving. No case repeatedly grows above 0.5%.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,392.00 | 29,392.00 | +0.00 |
| Integer loop / 1 | 1,079,424.40 | 1,079,424.64 | -0.24 |
| Positional calls / 1 | 523,752.96 | 523,752.96 | +0.00 |
| Keyword calls / 1 | 524,016.16 | 524,015.92 | +0.24 |
| Owned method calls / 1 | 3,591,118.80 | 3,554,792.08 | +36,326.72 |
| Full stable sort + output / 1 | 3,513,878.00 | 3,509,198.96 | +4,679.04 |
| ASCII pipeline / 1 | 461,800.00 | 461,800.00 | +0.00 |
| Empty / 2 | 29,224.00 | 29,224.00 | +0.00 |
| Integer loop / 2 | 1,079,210.64 | 1,079,210.64 | +0.00 |
| Positional calls / 2 | 523,583.92 | 523,555.52 | +28.40 |
| Keyword calls / 2 | 523,951.12 | 523,847.92 | +103.20 |
| Owned method calls / 2 | 3,590,367.76 | 3,508,447.68 | +81,920.08 |
| Full stable sort + output / 2 | 3,420,711.92 | 3,420,711.92 | +0.00 |
| ASCII pipeline / 2 | 461,800.00 | 461,800.00 | +0.00 |

## Separate native-code review

Both native groups follow all ordinary timing and allocation. Only JIT dump flags
change; ordinary tiering/GC remains. All 28 helper rows check complete output.
Their allocation rows stay separate from the ordinary allocation estimates.
The filter includes both lowered dispatchers, library MoveNext bodies, lowered
entry points, invocation/binder/lease helpers and executable interpretation.
All emitted filtered bodies, tiers, inline summaries and call targets remain in
[evidence](evidence.json); the table selects this experiment's direct boundaries.
An absent emitted tier is not evidence of zero cost.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,909 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Tier0 | 232 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier0 | 11,723 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier0 | 6,604 |
| baseline | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 822 |
| baseline | `PyFunction:ExecuteBody` | Tier0 | 324 |
| baseline | `ObjectSetAttrMethod:Invoke` | Tier0 | 1,169 |
| baseline | `ObjectGetAttrMethod:Invoke` | Tier0 | 624 |
| baseline | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,655 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Instrumented Tier0 | 203 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Instrumented Tier0 | 14,340 |
| baseline | `ObjectGetAttrMethod:Invoke` | Instrumented Tier0 | 718 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Instrumented Tier0 | 8,156 |
| baseline | `ObjectSetAttrMethod:Invoke` | Instrumented Tier0 | 1,335 |
| baseline | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| baseline | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 822 |
| baseline | `LythonRuntime:EvaluateLoweredExpression` | Tier1 | 864 |
| baseline | `<DispatchLoweredExpressionAsync>d__1016:MoveNext` | Tier1 | 12,013 |
| baseline | `ObjectGetAttrMethod:Invoke` | Tier1 | 4,037 |
| baseline | `<DispatchLoweredStatementAsync>d__1015:MoveNext` | Tier1 | 6,835 |
| baseline | `ObjectSetAttrMethod:Invoke` | Tier1 | 4,499 |
| baseline | `PyFunction:ExecuteBody` | Tier1 | 201 |
| baseline | `LythonRuntime:ExecuteStatements` | Tier1 | 2,162 |
| baseline | `ExecutableFrameInterpreter:Execute` | Tier1 | 4,935 |
| baseline | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,467 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,136 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 8,170 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier0 | 1,629 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 3,907 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Tier0 | 132 |
| candidate | `LythonRuntime:DispatchLoweredExpression` | Tier0 | 4,018 |
| candidate | `LythonRuntime:DispatchLoweredStatement` | Tier0 | 2,257 |
| candidate | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 741 |
| candidate | `PyFunction:ExecuteBody` | Tier0 | 324 |
| candidate | `ObjectSetAttrMethod:Invoke` | Tier0 | 1,169 |
| candidate | `ObjectGetAttrMethod:Invoke` | Tier0 | 624 |
| candidate | `ExecutableFrameInterpreter:Execute` | Instrumented Tier0 | 3,135 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1-OSR | 4,648 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Instrumented Tier0 | 1,932 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Instrumented Tier0 | 132 |
| candidate | `LythonRuntime:DispatchLoweredExpression` | Instrumented Tier0 | 4,689 |
| candidate | `ObjectGetAttrMethod:Invoke` | Instrumented Tier0 | 718 |
| candidate | `ObjectSetAttrMethod:Invoke` | Instrumented Tier0 | 1,335 |
| candidate | `PyFunction:ExecuteBody` | Instrumented Tier0 | 384 |
| candidate | `LythonRuntime:ExecuteStatements` | Instrumented Tier0 | 741 |
| candidate | `LythonRuntime:DispatchLoweredStatement` | Instrumented Tier0 | 2,664 |
| candidate | `LythonRuntime:EvaluateLoweredExpression` | Tier1 | 236 |
| candidate | `LythonRuntime:DispatchLoweredExpression` | Tier1 | 2,515 |
| candidate | `ObjectGetAttrMethod:Invoke` | Tier1 | 4,037 |
| candidate | `ObjectSetAttrMethod:Invoke` | Tier1 | 4,499 |
| candidate | `PyFunction:ExecuteBody` | Tier1 | 201 |
| candidate | `LythonRuntime:ExecuteStatements` | Tier1 | 461 |
| candidate | `LythonRuntime:DispatchLoweredStatement` | Tier1 | 1,247 |
| candidate | `ExecutableFrameInterpreter:Execute` | Tier1 | 4,933 |
| candidate | `LythonRuntime:TryExecuteSimpleReturn` | Tier1 | 4,861 |

Tier1 statement/expression entry bodies change 2,162→461 and 864→236 bytes;
candidate direct dispatch bodies are 1,247 / 2,515 bytes. Baseline async dispatch
MoveNext bodies are 6,835 / 12,013 bytes. Attribute get/set remain 4,037 / 4,499,
PyFunction body entry remains 201, executable Execute changes 4,935→4,933, and
simple return changes 4,467→4,861 with different inline summaries. This is emitted
code evidence, without a resident code-cache total or causal attribution of the
loop regression. No new CPU trace or qualified before/after CPU claim.

## Audit and next work

Independent audits recalculate all **593 normal responses**,
request sequences, full output hashes, medians/IQRs and producer identities.
They verify correctness gates precede timing, allocation/native ordering and
unchanged inputs. **20 owned services** are terminal, **66 recorded PIDs** absent,
both producers/helper rehashed and the shared VM lease freshly available.
Raw declarations, test receipts, all native listings and journals stay private
under `.git/agent-notes/lowered-sync-micros-20261010/`; maintained evidence includes
every timing row, both diagnostics and supporting hashes.

Candidate `d1a1c599` remains on `perf/lowered-sync-dispatch`. The prior receiver-
lease and unused-super-anchor decisions remain rejected. Further assessment
should examine actual attribute get/set handling and eligible executable method
bodies with independent descriptor, class-cell, scope, checkpoint/funding and
host-suspension proofs. The shared async path is a useful lead, but removing it
globally has failed this round's control gate. Original CSV/pipeline/C05 causes,
matched milestone/secondary qualification and Utf8Regex integration remain open.
